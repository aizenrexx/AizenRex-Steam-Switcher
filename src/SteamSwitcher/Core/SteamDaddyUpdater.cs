using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamSwitcher.Core;

/// <summary>Outcome of an auto-update pass.</summary>
public enum UpdateOutcome
{
    /// <summary>A newer release was found and installed.</summary>
    Updated,

    /// <summary>Already on the latest release. Nothing was done.</summary>
    AlreadyLatest,

    /// <summary>Skipped because a check already ran recently.</summary>
    SkippedRecentlyChecked,

    /// <summary>Disabled in settings.</summary>
    Disabled,

    /// <summary>The check or install failed.</summary>
    Failed
}

/// <summary>Result of an auto-update pass, including what to tell the user.</summary>
public sealed class UpdateResult
{
    public UpdateOutcome Outcome { get; init; }
    public string Message { get; init; } = "";
    public string? InstalledVersion { get; init; }
    public string? PreviousVersion { get; init; }

    /// <summary>True when the user should see a notification for this pass.</summary>
    public bool ShouldNotify => Outcome is UpdateOutcome.Updated or UpdateOutcome.Failed;

    public static UpdateResult Skip(UpdateOutcome outcome, string message) =>
        new() { Outcome = outcome, Message = message };
}

/// <summary>Persisted record of what we last saw and installed.</summary>
public sealed class UpdateState
{
    /// <summary>Release tag currently installed by us, e.g. "v3.2.1".</summary>
    public string InstalledVersion { get; set; } = "";

    /// <summary>SHA256 of the installed executable, when known.</summary>
    public string InstalledSha256 { get; set; } = "";

    /// <summary>Last time a version check actually hit the network.</summary>
    public DateTime LastCheckUtc { get; set; } = DateTime.MinValue;

    /// <summary>Last time an install completed.</summary>
    public DateTime LastInstallUtc { get; set; } = DateTime.MinValue;

    /// <summary>Path the executable was installed to.</summary>
    public string InstalledPath { get; set; } = "";
}

/// <summary>
/// Smart auto-update for SteamDaddy, run when switching to the Daddy profile.
///
/// Design rules, from how the upstream project actually ships:
///  1. The official installer ALWAYS downloads releases/latest with no version
///     check, so running it blindly re-downloads ~22 MB every single switch.
///     We ask the GitHub releases API for the tag first and only act on a real
///     change. That is what makes this "smart" rather than a repeated re-install.
///  2. Nothing runs twice for the same release: the installed tag and file hash
///     are recorded in data\steamdaddy-update.json, and a throttle window stops
///     repeated network checks during a run of quick switches.
///  3. The user is told when an update lands, including an ordinary one, and is
///     told nothing at all when there is no update.
///  4. Upstream requires clicking "Install Plugin" in the SteamDaddy UI after an
///     update, so that instruction is carried in the notification text.
/// </summary>
public static class SteamDaddyUpdater
{
    // The source lives in Settings so the user can repoint it if upstream
    // ever moves. Settings.DefaultSteamDaddyUpdateUrl holds the built-in
    // value, and EffectiveSteamDaddyUpdateUrl falls back to it when the
    // custom box is left empty.

    private const string AssetName = "SteamDaddy.exe";

    /// <summary>Minimum gap between two network checks.</summary>
    private static readonly TimeSpan CheckThrottle = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string StateFile => Path.Combine(AppPaths.DataDir, "steamdaddy-update.json");

    // ------------------------------------------------------------------ state

    public static UpdateState LoadState()
    {
        try
        {
            if (File.Exists(StateFile))
            {
                var json = File.ReadAllText(StateFile);
                var loaded = JsonSerializer.Deserialize<UpdateState>(json, JsonOptions);
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"steamdaddy-update.json unreadable, treating as first run: {ex.Message}");
        }

        return new UpdateState();
    }

    private static void SaveState(UpdateState state)
    {
        try
        {
            AppPaths.EnsureAll();
            var temp = StateFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
            if (File.Exists(StateFile)) File.Replace(temp, StateFile, null);
            else File.Move(temp, StateFile);
        }
        catch (Exception ex)
        {
            Log.Error("Could not save the SteamDaddy update state", ex);
        }
    }

    // ------------------------------------------------------------------- main

    /// <summary>
    /// Checks for a newer SteamDaddy release and installs it when one exists.
    /// Safe to call on every switch: it self-throttles and never repeats an
    /// install for a release it already applied.
    /// </summary>
    public static async Task<UpdateResult> RunAsync(
        Settings settings,
        IProgress<string>? progress = null,
        bool force = false,
        CancellationToken cancellation = default)
    {
        if (!settings.AutoUpdateSteamDaddy && !force)
            return UpdateResult.Skip(UpdateOutcome.Disabled, "SteamDaddy auto-update is switched off in Settings.");

        // One pass at a time, so two quick switches cannot race into a double install.
        if (!await Gate.WaitAsync(0, cancellation))
            return UpdateResult.Skip(UpdateOutcome.SkippedRecentlyChecked, "An update check is already running.");

        try
        {
            var state = LoadState();

            var age = DateTime.UtcNow - state.LastCheckUtc;
            if (!force && age < CheckThrottle)
            {
                var mins = (int)Math.Max(1, (CheckThrottle - age).TotalMinutes);
                Log.Info($"SteamDaddy update check skipped; last check was {(int)age.TotalMinutes} min ago.");
                return UpdateResult.Skip(
                    UpdateOutcome.SkippedRecentlyChecked,
                    $"Checked recently. Next check in about {mins} min.");
            }

            progress?.Report("Checking for a SteamDaddy update…");

            var release = await FetchLatestReleaseAsync(settings.EffectiveSteamDaddyUpdateUrl, cancellation);
            if (release is null)
                return new UpdateResult
                {
                    Outcome = UpdateOutcome.Failed,
                    Message = "Could not reach GitHub to check for a SteamDaddy update."
                };

            // Record that the network check happened, even when nothing changes.
            state.LastCheckUtc = DateTime.UtcNow;
            SaveState(state);

            var target = ResolveInstallPath(settings, state);

            // Nothing to do: same tag, and the file we installed is still there.
            var sameTag = string.Equals(state.InstalledVersion, release.Tag, StringComparison.OrdinalIgnoreCase);
            if (sameTag && !force && File.Exists(target))
            {
                Log.Info($"SteamDaddy is already on {release.Tag}; no update needed.");
                return new UpdateResult
                {
                    Outcome = UpdateOutcome.AlreadyLatest,
                    Message = $"SteamDaddy is already on {release.Tag}.",
                    InstalledVersion = release.Tag
                };
            }

            // A file already matching the published hash means the user updated
            // by hand. Adopt it instead of downloading the same bytes again.
            if (!force && File.Exists(target) && !string.IsNullOrEmpty(release.Sha256))
            {
                var onDisk = TryHash(target);
                if (string.Equals(onDisk, release.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    state.InstalledVersion = release.Tag;
                    state.InstalledSha256 = onDisk!;
                    state.InstalledPath = target;
                    SaveState(state);

                    Log.Info($"SteamDaddy on disk already matches {release.Tag}; recorded without downloading.");
                    return new UpdateResult
                    {
                        Outcome = UpdateOutcome.AlreadyLatest,
                        Message = $"SteamDaddy is already on {release.Tag}.",
                        InstalledVersion = release.Tag
                    };
                }
            }

            // ---- download and install
            var previous = string.IsNullOrWhiteSpace(state.InstalledVersion) ? null : state.InstalledVersion;
            progress?.Report($"Downloading SteamDaddy {release.Tag}…");

            var install = await DownloadAndInstallAsync(release, target, cancellation);
            if (!install.Success)
            {
                Log.Error($"SteamDaddy update to {release.Tag} failed: {install.Message}");
                Store.AddHistory("SteamDaddy update", false, install.Message);
                return new UpdateResult
                {
                    Outcome = UpdateOutcome.Failed,
                    Message = $"SteamDaddy {release.Tag} could not be installed: {install.Message}",
                    PreviousVersion = previous
                };
            }

            state.InstalledVersion = release.Tag;
            state.InstalledSha256 = TryHash(target) ?? "";
            state.InstalledPath = target;
            state.LastInstallUtc = DateTime.UtcNow;
            SaveState(state);

            var summary = previous is null
                ? $"SteamDaddy {release.Tag} installed."
                : $"SteamDaddy updated {previous} to {release.Tag}.";

            Log.Info(summary);
            Store.AddHistory("SteamDaddy update", true, summary);

            return new UpdateResult
            {
                Outcome = UpdateOutcome.Updated,
                Message = summary,
                InstalledVersion = release.Tag,
                PreviousVersion = previous
            };
        }
        catch (OperationCanceledException)
        {
            return UpdateResult.Skip(UpdateOutcome.Failed, "The update check was cancelled.");
        }
        catch (Exception ex)
        {
            Log.Error("SteamDaddy auto-update failed", ex);
            return new UpdateResult { Outcome = UpdateOutcome.Failed, Message = ex.Message };
        }
        finally
        {
            Gate.Release();
        }
    }

    // --------------------------------------------------------------- release

    private sealed record ReleaseInfo(string Tag, string DownloadUrl, string Sha256, long Size, string Notes);

    private static async Task<ReleaseInfo?> FetchLatestReleaseAsync(
        string releaseApiUrl, CancellationToken cancellation)
    {
        try
        {
            // Guard against a mistyped custom link rather than throwing.
            if (string.IsNullOrWhiteSpace(releaseApiUrl) ||
                !Uri.TryCreate(releaseApiUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                Log.Warn($"SteamDaddy update link is not a valid URL: '{releaseApiUrl}'");
                return null;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamSwitcher/2.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            Log.Debug($"Checking SteamDaddy releases at {uri}");
            var json = await http.GetStringAsync(uri, cancellation);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase)) continue;

                var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(url)) continue;

                var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0L;

                // "digest" arrives as "sha256:<hex>" when GitHub has computed it.
                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                var sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    ? digest["sha256:".Length..]
                    : "";

                return new ReleaseInfo(tag, url, sha, size, notes);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the latest SteamDaddy release: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Where SteamDaddy.exe lives. A path the user set wins; otherwise we keep
    /// the location we used last time; otherwise the Desktop, which is where
    /// the official installer puts it.
    /// </summary>
    private static string ResolveInstallPath(Settings settings, UpdateState state)
    {
        if (!string.IsNullOrWhiteSpace(settings.SteamDaddyExePath))
            return settings.SteamDaddyExePath;

        if (!string.IsNullOrWhiteSpace(state.InstalledPath))
            return state.InstalledPath;

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, AssetName);
    }

    private static async Task<OpResult> DownloadAndInstallAsync(
        ReleaseInfo release, string target, CancellationToken cancellation)
    {
        var temp = target + ".download";

        try
        {
            var folder = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamSwitcher/2.0");

                using var response = await http.GetAsync(
                    release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync(cancellation);
                await using var file = File.Create(temp);
                await source.CopyToAsync(file, cancellation);
            }

            // Verify before replacing anything.
            if (!string.IsNullOrEmpty(release.Sha256))
            {
                var actual = TryHash(temp);
                if (!string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(temp);
                    return OpResult.Fail("The download did not match the published checksum.");
                }
            }
            else if (release.Size > 0)
            {
                var actual = new FileInfo(temp).Length;
                if (actual != release.Size)
                {
                    TryDelete(temp);
                    return OpResult.Fail("The download was incomplete.");
                }
            }

            // Do not overwrite a running SteamDaddy.
            if (IsSteamDaddyRunning())
            {
                TryDelete(temp);
                return OpResult.Fail("SteamDaddy is running. Close it and switch again to finish the update.");
            }

            if (File.Exists(target))
            {
                var backup = target + ".previous";
                TryDelete(backup);
                try { File.Move(target, backup); }
                catch (Exception ex) { Log.Warn($"Could not keep a copy of the previous SteamDaddy: {ex.Message}"); }
            }

            File.Move(temp, target);
            return OpResult.Ok($"Installed to {target}");
        }
        catch (Exception ex)
        {
            TryDelete(temp);
            return OpResult.Fail(ex.Message);
        }
    }

    private static bool IsSteamDaddyRunning()
    {
        try { return Process.GetProcessesByName("SteamDaddy").Length > 0; }
        catch { return false; }
    }

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
