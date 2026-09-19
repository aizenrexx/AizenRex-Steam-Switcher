using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace SteamSwitcher.Core;

/// <summary>
/// Steam process handling through the .NET process APIs.
/// The old Python build shelled out to taskkill/tasklist; this talks to
/// Windows directly, so there is no console flash and no parsing of text output.
/// </summary>
public static class SteamProcesses
{
    /// <summary>Process names that hold file handles inside the Steam folder.</summary>
    public static readonly string[] Names =
    {
        "steam",
        "steamwebhelper",
        "GameOverlayUI",
        "steamservice",
        "steamerrorreporter",
        "steamerrorreporter64"
    };

    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSteamRunning() => Snapshot().Count > 0;

    /// <summary>All running Steam-related processes with memory usage.</summary>
    public static List<ProcessRow> Snapshot()
    {
        var rows = new List<ProcessRow>();

        foreach (var name in Names)
        {
            Process[] found;
            try { found = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (var process in found)
            {
                try
                {
                    rows.Add(new ProcessRow
                    {
                        Name = process.ProcessName + ".exe",
                        Pid = process.Id,
                        MemoryBytes = process.WorkingSet64
                    });
                }
                catch { /* process exited between enumeration and read */ }
                finally { process.Dispose(); }
            }
        }

        return rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Closes Steam: asks politely first, then forces, then waits for the
    /// handles to actually be released. Returns false only if something survives.
    /// </summary>
    public static async Task<OpResult> CloseSteamAsync(int timeoutSeconds = 20, IProgress<string>? progress = null)
    {
        if (!IsSteamRunning())
            return OpResult.Ok("Steam was not running.");

        progress?.Report("Asking Steam to close…");
        Log.Info("Closing Steam processes.");

        // 1. Graceful: steam.exe -shutdown lets Steam flush its own state.
        try
        {
            var steamExe = FindRunningSteamExe();
            if (steamExe is not null)
            {
                var info = new ProcessStartInfo(steamExe, "-shutdown")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var shutdown = Process.Start(info);
                if (shutdown is not null)
                    await shutdown.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(6));
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Graceful shutdown skipped: {ex.Message}");
        }

        // Give Steam a few seconds to leave on its own.
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && IsSteamRunning())
            await Task.Delay(400);

        // 2. Force anything that is left.
        if (IsSteamRunning())
        {
            progress?.Report("Forcing remaining Steam processes to stop…");
            foreach (var name in Names)
            {
                Process[] found;
                try { found = Process.GetProcessesByName(name); }
                catch { continue; }

                foreach (var process in found)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        Log.Info($"Killed {process.ProcessName} (pid {process.Id}).");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Could not kill {name}: {ex.Message}");
                    }
                    finally { process.Dispose(); }
                }
            }
        }

        // 3. Wait for full exit.
        deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (!IsSteamRunning())
            {
                // Windows needs a moment to release the file handles.
                progress?.Report("Waiting for file handles to release…");
                await Task.Delay(1200);
                Log.Info("All Steam processes stopped.");
                return OpResult.Ok("Steam closed.");
            }
            await Task.Delay(400);
        }

        var survivors = string.Join(", ", Snapshot().Select(p => $"{p.Name} ({p.Pid})"));
        Log.Error($"Steam did not exit in time. Still running: {survivors}");
        return OpResult.Fail($"Steam did not close in {timeoutSeconds}s. Still running: {survivors}");
    }

    /// <summary>Path of the steam.exe that is currently running, if any.</summary>
    private static string? FindRunningSteamExe()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("steam"))
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path)) return path;
                }
                catch { /* access denied for some modules */ }
                finally { process.Dispose(); }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>Launches Steam from the active folder.</summary>
    public static OpResult LaunchSteam(string steamDir)
    {
        var exe = Path.Combine(steamDir, "steam.exe");
        if (!File.Exists(exe))
            return OpResult.Fail($"steam.exe not found at {exe}");

        try
        {
            var info = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = steamDir
            };
            Process.Start(info);
            Log.Info($"Launched {exe}");
            return OpResult.Ok("Steam launched.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to launch Steam", ex);
            return OpResult.Fail($"Could not launch Steam: {ex.Message}");
        }
    }

    /// <summary>Launches a specific game through Steam.</summary>
    public static OpResult LaunchGame(string steamDir, string appId, string extraArgs = "")
    {
        var exe = Path.Combine(steamDir, "steam.exe");
        if (!File.Exists(exe))
            return OpResult.Fail($"steam.exe not found at {exe}");

        try
        {
            var args = $"-applaunch {appId}";
            if (!string.IsNullOrWhiteSpace(extraArgs)) args += " " + extraArgs.Trim();

            Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = true,
                WorkingDirectory = steamDir
            });

            Log.Info($"Launching app {appId}.");
            return OpResult.Ok($"Launching app {appId}.");
        }
        catch (Exception ex)
        {
            return OpResult.Fail($"Could not launch game: {ex.Message}");
        }
    }
}
