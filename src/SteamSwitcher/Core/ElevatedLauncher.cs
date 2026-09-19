using System.Diagnostics;
using System.IO;

namespace SteamSwitcher.Core;

/// <summary>
/// Starts Steam elevated without a UAC prompt each time.
///
/// Why a scheduled task:
/// This app runs elevated (requireAdministrator). A child process started from
/// an elevated parent normally inherits that elevated token, which is fine —
/// but Steam then runs as administrator permanently, and Steam itself warns
/// against that. Using ShellExecute with the "runas" verb shows a UAC prompt
/// every single switch, which is what the user is seeing.
///
/// Windows offers exactly one supported way to run something elevated with no
/// prompt: a Task Scheduler entry registered with RunLevel=HighestAvailable.
/// Creating the task needs admin once (we already have it), and after that the
/// task can be started silently. UAC is not bypassed or weakened — the consent
/// was given when the task was registered.
///
/// Nothing here disables UAC or edits its policy: that would lower the whole
/// machine's security, and is not something an app should do to a user's system.
/// </summary>
public static class ElevatedLauncher
{
    /// <summary>Name of the task we register. Stable, so we never create duplicates.</summary>
    public const string TaskName = "SteamProfileSwitcher_LaunchSteam";

    /// <summary>True when the helper task is already registered.</summary>
    public static bool IsInstalled()
    {
        try
        {
            var result = RunSchTasks($"/Query /TN \"{TaskName}\"");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers (or re-registers) the helper task so Steam can be started
    /// elevated without a prompt. Requires the app to be elevated right now.
    /// </summary>
    public static OpResult Install(string steamExePath)
    {
        if (!SteamProcesses.IsElevated())
            return OpResult.Fail("Administrator rights are needed once to set this up. Restart the app as administrator.");

        if (!File.Exists(steamExePath))
            return OpResult.Fail($"steam.exe not found at {steamExePath}");

        try
        {
            // Remove any previous version first, so an old path cannot linger.
            RunSchTasks($"/Delete /TN \"{TaskName}\" /F");

            // /RL HIGHEST = run with the highest available rights, no prompt.
            // /F overwrites, /IT keeps it interactive so Steam has a desktop.
            var args =
                $"/Create /TN \"{TaskName}\" /TR \"\\\"{steamExePath}\\\"\" " +
                "/SC ONCE /ST 00:00 /RL HIGHEST /F /IT";

            var result = RunSchTasks(args);

            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                Log.Error($"Could not register the elevated launch task: {detail}");
                return OpResult.Fail($"Could not set up silent elevation: {detail.Trim()}");
            }

            Log.Info($"Registered elevated launch task for {steamExePath}");
            return OpResult.Ok("Steam will now start with administrator rights without asking.");
        }
        catch (Exception ex)
        {
            Log.Error("Registering the elevated launch task failed", ex);
            return OpResult.Fail(ex.Message);
        }
    }

    /// <summary>Removes the helper task.</summary>
    public static OpResult Uninstall()
    {
        try
        {
            var result = RunSchTasks($"/Delete /TN \"{TaskName}\" /F");
            if (result.ExitCode != 0 && !result.Error.Contains("cannot find", StringComparison.OrdinalIgnoreCase))
                return OpResult.Fail(result.Error.Trim());

            Log.Info("Removed the elevated launch task.");
            return OpResult.Ok("Silent elevation turned off. Windows will ask again on each launch.");
        }
        catch (Exception ex)
        {
            return OpResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Starts Steam through the helper task, so no UAC prompt appears.
    /// Returns Fail when the task is missing or the path no longer matches,
    /// so the caller can fall back to a normal launch.
    /// </summary>
    public static OpResult LaunchViaTask(string steamExePath)
    {
        if (!IsInstalled())
            return OpResult.Fail("The silent-elevation task is not installed.");

        // The task stores a fixed path. If the active folder's steam.exe is not
        // the one registered, re-register before running it.
        if (!RegisteredPathMatches(steamExePath))
        {
            var refresh = Install(steamExePath);
            if (!refresh.Success) return refresh;
        }

        try
        {
            var result = RunSchTasks($"/Run /TN \"{TaskName}\"");
            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                return OpResult.Fail(detail.Trim());
            }

            Log.Info("Started Steam through the elevated task (no prompt).");
            return OpResult.Ok("Steam launched with administrator rights.");
        }
        catch (Exception ex)
        {
            return OpResult.Fail(ex.Message);
        }
    }

    /// <summary>Checks the task still points at the given steam.exe.</summary>
    private static bool RegisteredPathMatches(string steamExePath)
    {
        try
        {
            var result = RunSchTasks($"/Query /TN \"{TaskName}\" /FO LIST /V");
            if (result.ExitCode != 0) return false;

            // Compare on the folder, since the output wraps long lines.
            var folder = Path.GetDirectoryName(steamExePath) ?? "";
            return folder.Length > 0 &&
                   result.Output.Contains(folder, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private sealed record SchTasksResult(int ExitCode, string Output, string Error);

    private static SchTasksResult RunSchTasks(string arguments)
    {
        var info = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(info);
        if (process is null) return new SchTasksResult(-1, "", "Could not start schtasks.exe");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(20000);

        return new SchTasksResult(process.ExitCode, output, error);
    }
}
