using System.Diagnostics;
using System.IO;
using System.Text;

namespace SteamSwitcher.Core;

/// <summary>Maintenance helpers exposed on the Games &amp; Tools page.</summary>
public static class Tools
{
    /// <summary>Opens a folder in File Explorer.</summary>
    public static OpResult OpenFolder(string path)
    {
        if (!Directory.Exists(path))
            return OpResult.Fail($"Folder not found: {path}");

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            return OpResult.Ok($"Opened {path}");
        }
        catch (Exception ex)
        {
            return OpResult.Fail($"Could not open folder: {ex.Message}");
        }
    }

    /// <summary>Deletes Steam's browser cache so the client rebuilds it.</summary>
    public static OpResult ClearWebCache(string steamDir)
    {
        if (SteamProcesses.IsSteamRunning())
            return OpResult.Fail("Close Steam before clearing the web cache.");

        var targets = new List<string>
        {
            Path.Combine(steamDir, "config", "htmlcache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "htmlcache")
        };

        var removed = 0;
        var failures = new List<string>();

        foreach (var target in targets)
        {
            if (!Directory.Exists(target)) continue;
            try
            {
                Directory.Delete(target, recursive: true);
                removed++;
                Log.Info($"Cleared cache: {target}");
            }
            catch (Exception ex)
            {
                failures.Add($"{target}: {ex.Message}");
            }
        }

        if (removed == 0 && failures.Count == 0)
            return OpResult.Ok("No Steam web cache was present.");

        if (failures.Count > 0)
            return OpResult.Fail($"Cleared {removed}, failed {failures.Count}. {string.Join(" | ", failures)}");

        return OpResult.Ok($"Cleared {removed} cache location(s). Steam will rebuild it on next start.");
    }

    /// <summary>Runs SteamService /repair, which fixes most client service faults.</summary>
    public static async Task<OpResult> RepairSteamServiceAsync(string steamDir)
    {
        var exe = Path.Combine(steamDir, "bin", "SteamService.exe");
        if (!File.Exists(exe))
            return OpResult.Fail($"SteamService.exe not found at {exe}");

        try
        {
            using var process = Process.Start(new ProcessStartInfo(exe, "/repair")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null) return OpResult.Fail("Could not start SteamService.exe.");

            var timeout = Task.Delay(TimeSpan.FromSeconds(120));
            var finished = await Task.WhenAny(process.WaitForExitAsync(), timeout);

            if (finished == timeout)
                return OpResult.Fail("Steam service repair timed out after 120 seconds.");

            var output = (await process.StandardOutput.ReadToEndAsync()).Trim();
            var error = (await process.StandardError.ReadToEndAsync()).Trim();

            var message = output.Length > 0 ? output : error.Length > 0 ? error : $"Exit code {process.ExitCode}.";
            Log.Info($"Steam service repair finished: {message}");

            return process.ExitCode == 0 ? OpResult.Ok("Steam service repaired.") : OpResult.Fail(message);
        }
        catch (Exception ex)
        {
            Log.Error("Steam service repair failed", ex);
            return OpResult.Fail($"Repair failed: {ex.Message}");
        }
    }

    /// <summary>Writes the current library to a CSV inside &lt;App&gt;\exports.</summary>
    public static OpResult ExportLibraryCsv(SystemStatus status)
    {
        AppPaths.EnsureAll();
        var file = Path.Combine(AppPaths.ExportDir, $"library-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("Profile,Name,AppId,InstallDir,SizeBytes,SizeText,Redistributable");

            void Write(string profile, FolderReport? report)
            {
                if (report is null) return;
                foreach (var game in report.Games)
                {
                    builder.AppendLine(string.Join(',', new[]
                    {
                        Csv(profile), Csv(game.Name), Csv(game.AppId), Csv(game.InstallDir),
                        game.SizeBytes.ToString(), Csv(game.SizeText), game.IsRedistributable ? "yes" : "no"
                    }));
                }
            }

            Write("Main", status.Main);
            Write("Daddy", status.Daddy);

            File.WriteAllText(file, builder.ToString(), Encoding.UTF8);
            Log.Info($"Library exported to {file}");
            return OpResult.Ok($"Exported to exports\\{Path.GetFileName(file)}");
        }
        catch (Exception ex)
        {
            return OpResult.Fail($"Export failed: {ex.Message}");
        }
    }

    /// <summary>Copies the log file into &lt;App&gt;\exports.</summary>
    public static OpResult ExportLog()
    {
        AppPaths.EnsureAll();

        if (!File.Exists(AppPaths.LogFile))
            return OpResult.Fail("There is no log file yet.");

        var file = Path.Combine(AppPaths.ExportDir, $"log-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        try
        {
            File.Copy(AppPaths.LogFile, file, overwrite: true);
            return OpResult.Ok($"Log exported to exports\\{Path.GetFileName(file)}");
        }
        catch (Exception ex)
        {
            return OpResult.Fail($"Log export failed: {ex.Message}");
        }
    }

    private static string Csv(string value)
    {
        value ??= "";
        return value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
