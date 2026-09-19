using System.IO;

namespace SteamSwitcher.Core;

/// <summary>
/// The switching engine.
///
/// Fixes carried over from the Python build:
///  1. Every rename is journaled BEFORE it happens, so a crash or power loss
///     mid-switch can be detected and undone on the next start.
///  2. Rollback is verified, and a failed rollback is reported loudly instead
///     of leaving both folders parked under the wrong names.
///  3. An "Unknown" folder layout is a first-class state with a repair plan,
///     not an unexplained UI value.
///  4. Elevation is checked up front rather than failing on the first rename.
/// </summary>
public sealed class SwitchEngine
{
    private readonly Settings _settings;

    public SwitchEngine(Settings settings) => _settings = settings;

    /// <summary>
    /// Outcome of the SteamDaddy auto-update attempted during the most recent
    /// switch, or null when none was attempted. The UI reads this to decide
    /// whether to show an update notification.
    /// </summary>
    public UpdateResult? LastUpdateResult { get; private set; }

    // ------------------------------------------------------------------ status

    /// <summary>Reads the full machine state. Read-only.</summary>
    public SystemStatus ReadStatus()
    {
        var status = new SystemStatus
        {
            SteamFolderExists = Directory.Exists(_settings.SteamDir),
            MainFolderExists = Directory.Exists(_settings.MainDir),
            DaddyFolderExists = Directory.Exists(_settings.DaddyDir),
            IsElevated = SteamProcesses.IsElevated()
        };

        status.Active = status.SteamFolderExists ? SteamInspector.Inspect(_settings.SteamDir) : null;
        status.ActiveProfile = status.Active?.Kind ?? ProfileKind.Unknown;

        // The parked folders.
        //
        // The active profile lives in SteamDir, so its own parked folder is
        // SUPPOSED to be absent — that is what a completed switch looks like.
        // Each profile therefore resolves to the active report when it is the
        // one currently in play, and only otherwise to its parked folder.
        // Reading them in the old order made the active profile show
        // "Folder not found" with 0 games whenever its parked folder was gone.
        status.Main = status.ActiveProfile == ProfileKind.Main
            ? status.Active
            : (status.MainFolderExists ? SteamInspector.Inspect(_settings.MainDir) : null);

        status.Daddy = status.ActiveProfile == ProfileKind.Daddy
            ? status.Active
            : (status.DaddyFolderExists ? SteamInspector.Inspect(_settings.DaddyDir) : null);

        status.RunningProcesses = SteamProcesses.Snapshot();
        status.SteamRunning = status.RunningProcesses.Count > 0;

        // Layout sanity
        var plan = BuildRecoveryPlan(status);
        status.NeedsRecovery = plan.Count > 0;
        status.RecoveryHint = DescribeLayout(status, plan);

        return status;
    }

    private string DescribeLayout(SystemStatus status, List<JournalStep> plan)
    {
        if (!status.SteamFolderExists)
        {
            if (status.MainFolderExists && status.DaddyFolderExists)
                return "No active Steam folder, but both profiles are parked. Choose which one to activate.";
            if (plan.Count == 1)
                return $"A previous switch did not finish. One safe repair is available: restore {Path.GetFileName(plan[0].From)}.";
            return "No Steam folder found at the configured path. Check the paths in Settings.";
        }

        if (status.ActiveProfile == ProfileKind.Unknown)
            return "The active Steam folder could not be classified as Main or Daddy.";

        if (status.MainFolderExists && status.DaddyFolderExists)
            return "Unexpected layout: the active Steam folder exists alongside both parked profiles.";

        return "Folder layout is healthy.";
    }

    /// <summary>
    /// Works out whether the folders can be repaired with a single safe rename.
    /// Empty list means either healthy or unsafe to auto-repair.
    /// </summary>
    public List<JournalStep> BuildRecoveryPlan(SystemStatus status)
    {
        var plan = new List<JournalStep>();

        // Healthy: Steam exists and is classified.
        if (status.SteamFolderExists && status.ActiveProfile != ProfileKind.Unknown)
            return plan;

        // Steam missing, exactly one parked profile: activate it.
        if (!status.SteamFolderExists)
        {
            if (status.MainFolderExists && !status.DaddyFolderExists)
                plan.Add(new JournalStep { From = _settings.MainDir, To = _settings.SteamDir });
            else if (status.DaddyFolderExists && !status.MainFolderExists)
                plan.Add(new JournalStep { From = _settings.DaddyDir, To = _settings.SteamDir });
        }

        return plan;
    }

    /// <summary>
    /// Runs a crash recovery. Only ever performs the single safe rename from
    /// <see cref="BuildRecoveryPlan"/>; anything ambiguous is left to the user.
    /// </summary>
    public OpResult Recover()
    {
        var status = ReadStatus();
        var plan = BuildRecoveryPlan(status);

        if (plan.Count == 0)
            return status.SteamFolderExists
                ? OpResult.Ok("Nothing to recover; the layout is already valid.")
                : OpResult.Fail("This folder state cannot be repaired automatically. Fix the paths in Settings or rename manually.");

        var step = plan[0];
        if (!Directory.Exists(step.From) || Directory.Exists(step.To))
            return OpResult.Fail("The folders changed while preparing recovery. Refresh and try again.");

        try
        {
            Directory.Move(step.From, step.To);
            Store.ClearJournal();
            Log.Info($"Recovered: '{Path.GetFileName(step.From)}' restored as '{Path.GetFileName(step.To)}'.");
            Store.AddHistory("Recovery", true, $"Restored {Path.GetFileName(step.From)} to Steam.");
            return OpResult.Ok($"Recovered. {Path.GetFileName(step.From)} is active again.");
        }
        catch (Exception ex)
        {
            Log.Error("Recovery rename failed", ex);
            Store.AddHistory("Recovery", false, ex.Message);
            return OpResult.Fail($"Recovery failed: {ex.Message}");
        }
    }

    /// <summary>Detects an interrupted switch recorded in the journal.</summary>
    public SwitchJournal? PendingJournal()
    {
        var journal = Store.ReadJournal();
        return journal is { Completed: false } ? journal : null;
    }

    // ------------------------------------------------------------ safety check

    /// <summary>
    /// Pre-flight checks before a switch. Everything that can stop the switch
    /// is reported here, so the user sees the reason instead of a failed rename.
    /// </summary>
    public List<CheckRow> PreflightChecks(ProfileKind target, SystemStatus status)
    {
        var rows = new List<CheckRow>();

        void Add(string name, bool ok, string detail, CheckLevel? warnLevel = null) =>
            rows.Add(new CheckRow
            {
                Name = name,
                Detail = detail,
                Level = ok ? CheckLevel.Pass : (warnLevel ?? CheckLevel.Fail)
            });

        Add("Administrator rights", status.IsElevated,
            status.IsElevated ? "Running elevated." : "Restart the app as administrator.");

        var targetDir = target == ProfileKind.Main ? _settings.MainDir : _settings.DaddyDir;
        var targetIsActive = status.ActiveProfile == target;
        var targetReport = target == ProfileKind.Main ? status.Main : status.Daddy;

        Add("Target profile available", targetIsActive || Directory.Exists(targetDir),
            targetIsActive ? "Already the active profile." : targetDir);

        Add("Target differs from active", !targetIsActive,
            targetIsActive ? $"{Format.ProfileLabel(target)} is already active." : $"Active: {Format.ProfileLabel(status.ActiveProfile)}");

        Add("Base folder writable", CanWrite(_settings.BaseDir), _settings.BaseDir);

        // Account lock
        var expected = target == ProfileKind.Main ? _settings.MainExpectedSteamId : _settings.DaddyExpectedSteamId;
        var actual = targetReport?.Account?.SteamId64 ?? "";
        var lockOk = string.IsNullOrWhiteSpace(expected) || expected == actual;
        Add("Account lock", lockOk,
            string.IsNullOrWhiteSpace(expected)
                ? $"Not locked. Detected: {(actual.Length > 0 ? actual : "unknown")}"
                : $"Expected {expected}, found {(actual.Length > 0 ? actual : "nothing")}");

        // Free space is advisory only.
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_settings.BaseDir) ?? "C:\\");
            var free = drive.AvailableFreeSpace;
            Add("Free disk space", free > 1L * 1024 * 1024 * 1024,
                $"{Format.HumanSize(free)} available", CheckLevel.Warn);
        }
        catch
        {
            Add("Free disk space", true, "Not measured");
        }

        Add("Layout is clean", !status.NeedsRecovery, status.RecoveryHint, CheckLevel.Warn);

        return rows;
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return false;
            var probe = Path.Combine(folder, $".switcher-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ switch

    /// <summary>
    /// Switches to the requested profile.
    ///
    /// Sequence: preflight -> optional config backup -> close Steam ->
    /// journal -> park active -> activate target -> sync auto-login -> launch.
    /// Any failure after the first rename triggers a verified rollback.
    /// </summary>
    public async Task<OpResult> SwitchAsync(ProfileKind target, IProgress<string>? progress = null)
    {
        if (target is not (ProfileKind.Main or ProfileKind.Daddy))
            return OpResult.Fail("Choose either the Main or the Daddy profile.");

        var status = ReadStatus();

        // ---- gate on the blocking checks
        var checks = PreflightChecks(target, status);
        var blocking = checks.Where(c => c.Level == CheckLevel.Fail).ToList();
        if (blocking.Count > 0)
        {
            var reason = string.Join(" | ", blocking.Select(c => $"{c.Name}: {c.Detail}"));
            Log.Warn($"Switch to {target} blocked. {reason}");
            Store.AddHistory($"Switch to {Format.ProfileLabel(target)}", false, reason);
            return OpResult.Fail(reason);
        }

        var activeDir = _settings.SteamDir;
        var parkDir = status.ActiveProfile == ProfileKind.Daddy ? _settings.DaddyDir : _settings.MainDir;
        var targetDir = target == ProfileKind.Main ? _settings.MainDir : _settings.DaddyDir;

        // Work out the step numbering up front so every progress line can say
        // where it is in the run, e.g. "Step 2 of 5".
        var totalSteps = 2; // park + activate always happen
        if (_settings.BackupBeforeSwitch && Directory.Exists(activeDir)) totalSteps++;
        if (status.SteamRunning) totalSteps++;
        if (_settings.SyncAutoLogin) totalSteps++;
        if (target == ProfileKind.Daddy && _settings.AutoUpdateSteamDaddy) totalSteps++;
        if (_settings.AutoLaunchSteam) totalSteps++;

        var step = 0;
        void Step(string text, string estimate) =>
            progress?.Report($"Step {++step} of {totalSteps} · {text} (about {estimate})");

        // ---- optional backup of the config we are about to park
        if (_settings.BackupBeforeSwitch && Directory.Exists(activeDir))
        {
            Step("Backing up the current profile configuration", "5–15 seconds");
            var backup = BackupService.Create(activeDir, Format.ProfileLabel(status.ActiveProfile));
            Log.Info(backup.Success ? $"Pre-switch backup: {backup.Message}" : $"Pre-switch backup skipped: {backup.Message}");
        }

        // ---- close Steam
        if (status.SteamRunning) Step("Closing Steam", "5–20 seconds");
        var close = await SteamProcesses.CloseSteamAsync(progress: progress);
        if (!close.Success)
        {
            Store.AddHistory($"Switch to {Format.ProfileLabel(target)}", false, close.Message);
            return close;
        }

        // ---- journal the plan before touching the disk
        var journal = new SwitchJournal
        {
            From = status.ActiveProfile,
            To = target,
            Steps = new List<JournalStep>
            {
                new() { From = activeDir, To = parkDir },
                new() { From = targetDir, To = activeDir }
            }
        };
        Store.WriteJournal(journal);

        // ---- step 1: park the currently active folder
        var parkedSomething = false;
        if (Directory.Exists(activeDir))
        {
            Step($"Parking the active profile as {Path.GetFileName(parkDir)}", "2–10 seconds");

            if (Directory.Exists(parkDir))
            {
                Store.ClearJournal();
                var msg = $"Cannot park the active profile: '{parkDir}' already exists. Move or remove it first.";
                Log.Error(msg);
                Store.AddHistory($"Switch to {Format.ProfileLabel(target)}", false, msg);
                return OpResult.Fail(msg);
            }

            var park = SafeMove(activeDir, parkDir);
            if (!park.Success)
            {
                Store.ClearJournal();
                Store.AddHistory($"Switch to {Format.ProfileLabel(target)}", false, park.Message);
                return park;
            }

            journal.Steps[0].Done = true;
            Store.WriteJournal(journal);
            parkedSomething = true;
        }

        // ---- step 2: activate the target folder
        Step($"Activating {Format.ProfileLabel(target)}", "2–10 seconds");

        var activate = SafeMove(targetDir, activeDir);
        if (!activate.Success)
        {
            // Rollback, and verify it actually worked.
            var rollbackNote = "";
            if (parkedSomething)
            {
                progress?.Report("Rolling back…");
                var rollback = SafeMove(parkDir, activeDir);
                rollbackNote = rollback.Success
                    ? " The previous profile was restored."
                    : $" ROLLBACK FAILED: the original profile is still parked at '{parkDir}'. Use Repair on the Diagnostics page.";

                if (rollback.Success)
                {
                    journal.Steps[0].Done = false;
                    Store.ClearJournal();
                }
                else
                {
                    Log.Error($"Rollback failed. Manual state: active missing, parked at {parkDir}.");
                }
            }
            else
            {
                Store.ClearJournal();
            }

            var message = activate.Message + rollbackNote;
            Store.AddHistory($"Switch to {Format.ProfileLabel(target)}", false, message);
            return OpResult.Fail(message);
        }

        journal.Steps[1].Done = true;
        journal.Completed = true;
        Store.WriteJournal(journal);
        Store.ClearJournal();

        var result = $"Switched to {Format.ProfileLabel(target)}.";
        Log.Info(result);

        // ---- step 3: auto-login sync
        if (_settings.SyncAutoLogin)
        {
            progress?.Report("Syncing the sign-in account…");
            var sync = AutoLoginSync.SyncFor(activeDir);
            if (sync.Success) result += $" {sync.Message}";
            else Log.Warn($"Auto-login sync: {sync.Message}");
        }

        // ---- step 4: SteamDaddy smart auto-update
        //
        // Only when moving to the Daddy profile, and only before Steam starts,
        // so the patcher is never swapped underneath a running client. The
        // updater self-throttles and skips when the release has not changed,
        // so a run of quick switches does not re-download anything.
        if (target == ProfileKind.Daddy && _settings.AutoUpdateSteamDaddy)
        {
            var update = await SteamDaddyUpdater.RunAsync(_settings, progress);
            LastUpdateResult = update;

            if (update.Outcome == UpdateOutcome.Updated)
                result += $" {update.Message}";
            else if (update.Outcome == UpdateOutcome.Failed)
                result += $" SteamDaddy update did not finish: {update.Message}";
        }
        else
        {
            LastUpdateResult = null;
        }

        // ---- step 5: launch
        if (_settings.AutoLaunchSteam)
        {
            Step("Starting Steam", "3–10 seconds");
            await Task.Delay(600);

            OpResult launch;

            // Prefer the registered scheduled task, which starts Steam elevated
            // without a UAC prompt. Fall back to a normal launch if that is not
            // set up or fails, so a switch never ends without Steam running.
            if (_settings.SilentElevation)
            {
                var exe = Path.Combine(activeDir, "steam.exe");
                launch = ElevatedLauncher.LaunchViaTask(exe);

                if (!launch.Success)
                {
                    Log.Warn($"Silent elevation unavailable ({launch.Message}); using a normal launch.");
                    launch = SteamProcesses.LaunchSteam(activeDir);
                }
            }
            else
            {
                launch = SteamProcesses.LaunchSteam(activeDir);
            }

            result += launch.Success ? " Steam is starting." : $" Steam was not started ({launch.Message}).";
        }

        Store.AddHistory($"Switch to {Format.ProfileLabel(target)}", true, result);
        return OpResult.Ok(result);
    }

    /// <summary>
    /// Directory.Move with retries: Windows can hold a folder for a moment
    /// after the last process exits.
    /// </summary>
    private static OpResult SafeMove(string source, string destination, int attempts = 12, int delayMs = 600)
    {
        if (!Directory.Exists(source))
            return OpResult.Fail($"Source folder does not exist: {source}");

        if (Directory.Exists(destination))
            return OpResult.Fail($"Destination already exists: {destination}");

        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                Directory.Move(source, destination);
                Log.Info($"Renamed '{Path.GetFileName(source)}' to '{Path.GetFileName(destination)}'.");
                return OpResult.Ok($"Renamed to {Path.GetFileName(destination)}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                last = ex;
                Log.Warn($"Rename attempt {attempt}/{attempts} failed: {ex.Message}");
                Thread.Sleep(delayMs);
            }
            catch (Exception ex)
            {
                Log.Error($"Rename '{source}' -> '{destination}' failed", ex);
                return OpResult.Fail($"Rename failed: {ex.Message}");
            }
        }

        return OpResult.Fail($"Could not rename after {attempts} attempts: {last?.Message}");
    }
}
