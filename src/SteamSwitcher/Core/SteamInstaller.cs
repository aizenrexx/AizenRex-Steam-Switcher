using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Win32;

namespace SteamSwitcher.Core;

/// <summary>
/// Finds Steam on the machine, and installs it when it is missing.
/// Used by the first-run wizard so a brand-new PC needs nothing but this app.
/// </summary>
public static class SteamInstaller
{
    /// <summary>Valve's official installer. Same file the website hands out.</summary>
    public const string SetupUrl = "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe";

    public sealed record Detection(bool Installed, string? Path, string Source);

    /// <summary>
    /// Locates a REAL Steam install. The registry alone is not trusted: an
    /// uninstall can leave keys behind with a stale or empty path, which is
    /// exactly what this machine shows. steam.exe must exist on disk.
    /// </summary>
    public static Detection Detect()
    {
        foreach (var (path, source) in CandidatePaths())
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            try
            {
                var exe = Path.Combine(path, "steam.exe");
                if (File.Exists(exe))
                    return new Detection(true, Path.GetFullPath(path), source);
            }
            catch
            {
                // Malformed path in the registry; just try the next candidate.
            }
        }

        return new Detection(false, null, "not found");
    }

    private static IEnumerable<(string? Path, string Source)> CandidatePaths()
    {
        yield return (ReadReg(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"), "HKCU SteamPath");
        yield return (ReadReg(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"), "HKLM InstallPath");
        yield return (ReadReg(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"), "HKLM InstallPath (64)");
        yield return (@"C:\Program Files (x86)\Steam", "default path");
        yield return (@"C:\Program Files\Steam", "default path (64)");
    }

    private static string? ReadReg(RegistryKey hive, string subKey, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads SteamSetup.exe and runs it silently.
    /// <para>
    /// "/S" is Valve's NSIS silent switch. "/D=" sets the target directory and
    /// must be the LAST argument and unquoted — that is an NSIS requirement,
    /// not a style choice; quoting it makes the installer ignore it.
    /// </para>
    /// </summary>
    public static async Task<OpResult> InstallAsync(
        string? targetDir,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        var existing = Detect();
        if (existing.Installed)
            return OpResult.Ok($"Steam is already installed at {existing.Path}.");

        string setup;
        try
        {
            setup = Path.Combine(Path.GetTempPath(), "SteamSetup.exe");

            progress?.Report("Downloading Steam installer…");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamProfileSwitcher");

            using var response = await http.GetAsync(SetupUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using (var src = await response.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(setup))
            {
                await src.CopyToAsync(dst, ct);
            }

            var size = new FileInfo(setup).Length;
            if (size < 500_000)
                return OpResult.Fail($"The downloaded installer looks wrong ({size} bytes).");

            Log.Info($"SteamSetup.exe downloaded: {size} bytes");
        }
        catch (OperationCanceledException)
        {
            return OpResult.Fail("Download cancelled.");
        }
        catch (Exception ex)
        {
            Log.Error("Steam installer download failed", ex);
            return OpResult.Fail($"Could not download Steam: {ex.Message}");
        }

        try
        {
            progress?.Report("Installing Steam…");

            // /D must come last and must NOT be quoted (NSIS rule).
            var args = string.IsNullOrWhiteSpace(targetDir)
                ? "/S"
                : $"/S /D={targetDir.TrimEnd('\\')}";

            var psi = new ProcessStartInfo
            {
                FileName = setup,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            if (proc is null) return OpResult.Fail("Could not start the Steam installer.");

            await proc.WaitForExitAsync(ct);
            Log.Info($"SteamSetup exited with code {proc.ExitCode}");

            // Never trust the exit code alone — verify steam.exe really exists.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var check = Detect();
                if (check.Installed)
                {
                    progress?.Report("Steam installed.");
                    return OpResult.Ok($"Steam installed at {check.Path}.");
                }
                await Task.Delay(1000, ct);
            }

            return OpResult.Fail("The installer finished but steam.exe was not found.");
        }
        catch (OperationCanceledException)
        {
            return OpResult.Fail("Installation cancelled.");
        }
        catch (Exception ex)
        {
            Log.Error("Steam installation failed", ex);
            return OpResult.Fail($"Could not install Steam: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(setup)) File.Delete(setup); } catch { /* temp file */ }
        }
    }
}
