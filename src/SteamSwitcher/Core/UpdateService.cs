using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace SteamSwitcher.Core;

/// <summary>Outcome of a self-update check.</summary>
public enum UpdateStatus
{
    /// <summary>The running build is the newest published release.</summary>
    UpToDate,

    /// <summary>A newer release exists and can be installed.</summary>
    UpdateAvailable,

    /// <summary>The check could not complete (offline, rate limit, no release).</summary>
    Failed
}

/// <summary>What a self-update check found.</summary>
public sealed class UpdateCheck
{
    public UpdateStatus Status { get; init; } = UpdateStatus.Failed;
    public string Message { get; init; } = "";

    /// <summary>Version of the newest published release, without the leading v.</summary>
    public string LatestVersion { get; init; } = "";

    /// <summary>Release notes body, trimmed for display.</summary>
    public string Notes { get; init; } = "";

    public string DownloadUrl { get; init; } = "";
    public string AssetName { get; init; } = "";
    public long SizeBytes { get; init; }
    public string Sha256 { get; init; } = "";

    /// <summary>True when an installable update was found.</summary>
    public bool HasUpdate => Status == UpdateStatus.UpdateAvailable && DownloadUrl.Length > 0;

    public string SizeText => SizeBytes > 0 ? Format.HumanSize(SizeBytes) : "size unknown";

    /// <summary>True when the only asset offered was the portable ZIP.</summary>
    public bool IsPortableOnly =>
        AssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Self-update for AizenRex Steam Switcher itself.
///
/// Distinct from <see cref="SteamDaddyUpdater"/>, which updates the external
/// SteamDaddy tool. This one asks the project's own GitHub releases API what
/// the newest published version is, compares it with
/// <see cref="AppInfo.CurrentVersion"/>, and can download and launch the
/// published installer.
///
/// Design rules:
///  1. The version is compared, not assumed. A user on a newer build is never
///     told to "update" to an older one.
///  2. The installer asset is preferred, because Inno Setup already handles
///     in-place upgrade and preserves the user's data folder. The portable ZIP
///     is only offered when no installer was published.
///  3. Downloads are verified against the SHA256 GitHub publishes before
///     anything is executed.
/// </summary>
public static class UpdateService
{
    /// <summary>How many characters of release notes to surface in the UI.</summary>
    private const int NotesLimit = 600;

    public static string CurrentVersion => AppInfo.CurrentVersion;

    /// <summary>Asks GitHub for the newest release and compares it with this build.</summary>
    public static async Task<UpdateCheck> CheckAsync(CancellationToken cancellation = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"AizenRex-Steam-Switcher/{AppInfo.CurrentVersion}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(AppInfo.LatestReleaseApiUrl, cancellation);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var latest = Normalise(tag);

            if (latest.Length == 0)
                return new UpdateCheck
                {
                    Status = UpdateStatus.Failed,
                    Message = "The latest release does not carry a version tag."
                };

            if (Compare(latest, AppInfo.CurrentVersion) <= 0)
                return new UpdateCheck
                {
                    Status = UpdateStatus.UpToDate,
                    LatestVersion = latest,
                    Message = $"You are on the newest version (v{AppInfo.CurrentVersion})."
                };

            // Prefer the installer; fall back to the portable ZIP.
            var asset = PickAsset(root, "-Setup-v", ".exe") ?? PickAsset(root, "Portable", ".zip");

            if (asset is null)
                return new UpdateCheck
                {
                    Status = UpdateStatus.UpdateAvailable,
                    LatestVersion = latest,
                    Notes = Trim(notes),
                    Message = $"v{latest} is out, but this release has no downloadable package."
                };

            return new UpdateCheck
            {
                Status = UpdateStatus.UpdateAvailable,
                LatestVersion = latest,
                Notes = Trim(notes),
                DownloadUrl = asset.Url,
                AssetName = asset.Name,
                SizeBytes = asset.Size,
                Sha256 = asset.Sha256,
                Message = $"v{latest} is available ({asset.Name}, {Format.HumanSize(asset.Size)})."
            };
        }
        catch (OperationCanceledException)
        {
            return new UpdateCheck { Status = UpdateStatus.Failed, Message = "The update check was cancelled." };
        }
        catch (Exception ex)
        {
            Log.Warn($"Update check failed: {ex.Message}");
            return new UpdateCheck
            {
                Status = UpdateStatus.Failed,
                Message = "Could not reach GitHub. Check your connection and try again."
            };
        }
    }

    /// <summary>
    /// Downloads the published package into the app's own folder and verifies
    /// its checksum. Returns the path to the downloaded file.
    /// </summary>
    public static async Task<OpResult> DownloadAsync(
        UpdateCheck check,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default)
    {
        if (!check.HasUpdate)
            return OpResult.Fail("There is no update to download.");

        var target = Path.Combine(AppPaths.ExportDir, check.AssetName);

        try
        {
            AppPaths.EnsureAll();
            Directory.CreateDirectory(AppPaths.ExportDir);

            progress?.Report($"Downloading {check.AssetName}...");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"AizenRex-Steam-Switcher/{AppInfo.CurrentVersion}");

                using var response = await http.GetAsync(
                    check.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync(cancellation);
                await using var file = File.Create(target);
                await source.CopyToAsync(file, cancellation);
            }

            if (!string.IsNullOrEmpty(check.Sha256))
            {
                var actual = TryHash(target);
                if (!string.Equals(actual, check.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(target);
                    return OpResult.Fail("The download did not match the published checksum, so it was discarded.");
                }
            }

            Log.Info($"Downloaded update {check.AssetName} to {target}");
            return OpResult.Ok(target);
        }
        catch (Exception ex)
        {
            TryDelete(target);
            Log.Error("Update download failed", ex);
            return OpResult.Fail($"Download failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a downloaded installer silently. The caller is expected to shut the
    /// app down straight afterwards so the files can be replaced.
    /// </summary>
    public static OpResult LaunchInstaller(string installerPath)
    {
        try
        {
            if (!File.Exists(installerPath))
                return OpResult.Fail("The downloaded installer is no longer on disk.");

            var info = new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /NORESTART /SP-"
            };

            Process.Start(info);
            Log.Info($"Launched installer {installerPath}");
            return OpResult.Ok("The installer is running. This window will now close.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not launch the downloaded installer", ex);
            return OpResult.Fail($"Could not start the installer: {ex.Message}");
        }
    }

    /// <summary>Opens the releases page, for the portable ZIP route.</summary>
    public static void OpenReleasesPage() => OpenUrl(AppInfo.ReleasesUrl);

    /// <summary>Opens an https link in the default browser.</summary>
    public static void OpenUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open {url}: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ assets

    private sealed record Asset(string Name, string Url, long Size, string Sha256);

    private static Asset? PickAsset(JsonElement root, string nameFragment, string extension)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (name.Length == 0) continue;
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase)) continue;

            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            if (url.Length == 0) continue;

            var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0L;

            // "digest" arrives as "sha256:<hex>" once GitHub has computed it.
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            var sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? digest["sha256:".Length..]
                : "";

            return new Asset(name, url, size, sha);
        }

        return null;
    }

    // --------------------------------------------------------------- versions

    /// <summary>Strips a leading v/V and any whitespace.</summary>
    private static string Normalise(string tag)
    {
        var value = (tag ?? "").Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value[1..];
        return value.Trim();
    }

    /// <summary>Compares dotted numeric versions. Returns &lt;0, 0 or &gt;0.</summary>
    public static int Compare(string left, string right)
    {
        var a = Parse(left);
        var b = Parse(right);
        var length = Math.Max(a.Length, b.Length);

        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }

        return 0;
    }

    private static int[] Parse(string version)
    {
        var parts = (version ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            // Tolerate a suffix such as "2.2.0-beta" by reading the leading digits.
            var digits = new string(parts[i].TakeWhile(char.IsDigit).ToArray());
            result[i] = int.TryParse(digits, out var value) ? value : 0;
        }

        return result;
    }

    private static string Trim(string notes)
    {
        var text = (notes ?? "").Replace("\r\n", "\n").Trim();
        if (text.Length <= NotesLimit) return text;
        return text[..NotesLimit].TrimEnd() + "...";
    }

    // ----------------------------------------------------------------- helpers

    private static string? TryHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}