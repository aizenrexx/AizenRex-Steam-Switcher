using System.Diagnostics;
using System.IO;

namespace SteamSwitcher.Core;

/// <summary>How the user wants their profiles laid out.</summary>
public enum ProfileLayout
{
    /// <summary>Main + Daddy, the normal setup.</summary>
    Dual,
    /// <summary>One profile only.</summary>
    Single
}

/// <summary>
/// Drives first-run setup on a machine that may have nothing installed:
/// installs Steam, creates the profile folders, and walks the user through
/// signing in once per profile.
/// </summary>
public static class FirstRunService
{
    /// <summary>
    /// True when setup has never completed. Deliberately based on our own
    /// marker plus real folders on disk, never on the Steam registry alone,
    /// because an uninstall leaves stale keys behind.
    /// </summary>
    public static bool NeedsSetup(Settings s)
    {
        try
        {
            if (File.Exists(SetupMarkerPath)) return false;

            var mainReady = Directory.Exists(s.MainDir);
            var daddyReady = Directory.Exists(s.DaddyDir);
            var liveReady = Directory.Exists(s.SteamDir);

            // Nothing to switch between yet.
            return !(liveReady && (mainReady || daddyReady));
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not determine setup state: {ex.Message}");
            return false; // never trap the user in the wizard
        }
    }

    public static string SetupMarkerPath => Path.Combine(AppPaths.DataDir, "setup-complete.json");

    public static void MarkComplete()
    {
        try
        {
            AppPaths.EnsureAll();
            File.WriteAllText(SetupMarkerPath,
                $"{{\"completedUtc\":\"{DateTime.UtcNow:o}\"}}");
            Log.Info("First-run setup marked complete.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not write the setup marker: {ex.Message}");
        }
    }

    /// <summary>Step 1 — make sure Steam exists, installing it when it does not.</summary>
    public static Task<OpResult> EnsureSteamAsync(
        Settings s, IProgress<string>? progress, CancellationToken ct = default)
        => SteamInstaller.InstallAsync(s.SteamDir, progress, ct);

    /// <summary>
    /// Step 2 — create the profile folders the chosen layout needs.
    /// Only empty folders are created; existing ones are adopted untouched.
    /// </summary>
    public static OpResult CreateProfileFolders(Settings s, ProfileLayout layout)
    {
        try
        {
            var wanted = new List<string> { s.MainDir };
            if (layout == ProfileLayout.Dual) wanted.Add(s.DaddyDir);

            var made = new List<string>();
            foreach (var dir in wanted)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    made.Add(Path.GetFileName(dir));
                }
            }

            return OpResult.Ok(made.Count == 0
                ? "Profile folders already exist."
                : $"Created: {string.Join(", ", made)}.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not create profile folders", ex);
            return OpResult.Fail($"Could not create the profile folders: {ex.Message}");
        }
    }

    /// <summary>
    /// Step 3 — launch Steam so the user can sign in, then wait for the new
    /// account to appear in loginusers.vdf.
    /// <para>
    /// The user signs in themselves, with Steam's own "Remember my password"
    /// ticked. We never see or store the password: Steam keeps a refresh
    /// token, which is why this is needed only once per account.
    /// </para>
    /// </summary>
    public static async Task<(OpResult Result, StoredLogin? Account)> LoginToProfileAsync(
        string steamDir,
        IProgress<string>? progress,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        try
        {
            var known = LoginUsers.Read(steamDir).Select(a => a.SteamId64).ToHashSet();

            var exe = Path.Combine(steamDir, "steam.exe");
            if (!File.Exists(exe))
                return (OpResult.Fail($"steam.exe not found in {steamDir}."), null);

            progress?.Report("Opening Steam — please sign in, and tick \"Remember my password\".");

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = steamDir,
                UseShellExecute = true
            });

            var account = await LoginUsers.WaitForNewLoginAsync(steamDir, known, timeout, ct);

            if (account is null)
                return (OpResult.Fail("No new sign-in was detected. You can finish this later."), null);

            // Make this the unambiguous auto-login account for the folder.
            LoginUsers.SetActiveAccount(steamDir, account.AccountName);

            progress?.Report($"Signed in as {account.PersonaName}.");
            return (OpResult.Ok($"Signed in as {account.PersonaName} ({account.AccountName})."), account);
        }
        catch (Exception ex)
        {
            Log.Error("Profile login step failed", ex);
            return (OpResult.Fail($"Sign-in step failed: {ex.Message}"), null);
        }
    }

    /// <summary>
    /// Records which account belongs to which profile, so the Dashboard can
    /// identify the active profile instead of showing "Folder not found".
    /// </summary>
    public static void RememberAccount(Settings s, bool isMain, StoredLogin account)
    {
        if (isMain) s.MainExpectedSteamId = account.SteamId64;
        else s.DaddyExpectedSteamId = account.SteamId64;

        Store.SaveSettings(s);
        Log.Info($"Recorded {account.AccountName} for {(isMain ? "Main" : "Daddy")}.");
    }
}
