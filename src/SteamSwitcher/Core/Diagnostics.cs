using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace SteamSwitcher.Core;

/// <summary>Health checks, a numeric score, and profile comparison.</summary>
public static class Diagnostics
{
    public sealed class HealthReport
    {
        public int Score { get; set; }
        public string Grade { get; set; } = "";
        public List<CheckRow> Checks { get; set; } = new();
        public List<string> Issues { get; set; } = new();
    }

    public static HealthReport Run(Settings settings, SystemStatus status)
    {
        var checks = new List<CheckRow>();

        void Add(string name, bool ok, string detail, CheckLevel? failLevel = null) =>
            checks.Add(new CheckRow
            {
                Name = name,
                Detail = detail,
                Level = ok ? CheckLevel.Pass : (failLevel ?? CheckLevel.Fail)
            });

        // Elevation
        Add("Administrator rights", status.IsElevated,
            status.IsElevated ? "Running elevated." : "Folder renames need administrator rights.");

        // Paths
        Add("Base folder", Directory.Exists(settings.BaseDir), settings.BaseDir);
        Add("Active Steam folder", status.SteamFolderExists, settings.SteamDir);

        var mainAvailable = status.MainFolderExists || status.ActiveProfile == ProfileKind.Main;
        var daddyAvailable = status.DaddyFolderExists || status.ActiveProfile == ProfileKind.Daddy;

        Add("Main profile available", mainAvailable,
            status.ActiveProfile == ProfileKind.Main ? $"{settings.SteamDir} (active)" : settings.MainDir);

        Add("Daddy profile available", daddyAvailable,
            status.ActiveProfile == ProfileKind.Daddy ? $"{settings.SteamDir} (active)" : settings.DaddyDir);

        Add("Profile classified", status.ActiveProfile != ProfileKind.Unknown,
            $"Detected: {Format.ProfileLabel(status.ActiveProfile)}");

        // steam.exe
        var steamExe = Path.Combine(settings.SteamDir, "steam.exe");
        Add("Steam executable", File.Exists(steamExe), steamExe);

        // Accounts
        foreach (var (label, report) in new[] { ("Main", status.Main), ("Daddy", status.Daddy) })
        {
            var account = report?.Account;
            Add($"{label} sign-in account", account is not null && account.AccountName.Length > 0,
                account is not null ? $"{account.Display} ({account.SteamId64})" : "loginusers.vdf not readable",
                CheckLevel.Warn);
        }

        // Duplicate manifests
        foreach (var (label, report) in new[] { ("Main", status.Main), ("Daddy", status.Daddy) })
        {
            if (report is null) continue;
            var ids = report.Games.Select(g => g.AppId).ToList();
            var hasDuplicates = ids.Count != ids.Distinct().Count();
            Add($"{label} app manifests", !hasDuplicates,
                hasDuplicates ? "Duplicate appmanifest entries found." : $"{report.TotalAppCount} manifests, no duplicates.",
                CheckLevel.Warn);
        }

        // Disk space
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(settings.BaseDir) ?? "C:\\");
            var free = drive.AvailableFreeSpace;
            Add("Free disk space", free >= 2L * 1024 * 1024 * 1024, $"{Format.HumanSize(free)} available", CheckLevel.Warn);
        }
        catch (Exception ex)
        {
            Add("Free disk space", false, ex.Message, CheckLevel.Warn);
        }

        // Layout conflict
        Add("Folder layout", !status.NeedsRecovery, status.RecoveryHint,
            status.SteamFolderExists ? CheckLevel.Warn : CheckLevel.Fail);

        // Log size
        var logBytes = Log.CurrentSizeBytes();
        Add("Log file size", logBytes < 2_000_000, $"{Format.HumanSize(logBytes)} (rotates at 1 MB)", CheckLevel.Warn);

        // Score
        var score = 100;
        var issues = new List<string>();
        foreach (var check in checks)
        {
            if (check.Level == CheckLevel.Fail) { score -= 12; issues.Add($"{check.Name}: {check.Detail}"); }
            else if (check.Level == CheckLevel.Warn) { score -= 5; issues.Add($"{check.Name}: {check.Detail}"); }
        }
        score = Math.Clamp(score, 0, 100);

        return new HealthReport
        {
            Score = score,
            Grade = score >= 90 ? "Excellent" : score >= 75 ? "Good" : score >= 50 ? "Needs attention" : "Critical",
            Checks = checks,
            Issues = issues
        };
    }

    // ------------------------------------------------------------- comparison

    public sealed class ComparisonReport
    {
        public int MainGames { get; set; }
        public int DaddyGames { get; set; }
        public int MainMods { get; set; }
        public int DaddyMods { get; set; }
        public int MainManifests { get; set; }
        public int DaddyManifests { get; set; }
        public long MainSize { get; set; }
        public long DaddySize { get; set; }
        public List<string> SharedGames { get; set; } = new();
        public List<string> MainOnly { get; set; } = new();
        public List<string> DaddyOnly { get; set; } = new();

        public string MainSizeText => Format.HumanSize(MainSize);
        public string DaddySizeText => Format.HumanSize(DaddySize);
    }

    public static ComparisonReport Compare(SystemStatus status)
    {
        var main = status.Main;
        var daddy = status.Daddy;

        var mainGames = (main?.Games ?? new List<GameInfo>())
            .Where(g => !g.IsRedistributable)
            .ToDictionary(g => g.AppId, g => g.Name, StringComparer.OrdinalIgnoreCase);

        var daddyGames = (daddy?.Games ?? new List<GameInfo>())
            .Where(g => !g.IsRedistributable)
            .ToDictionary(g => g.AppId, g => g.Name, StringComparer.OrdinalIgnoreCase);

        var shared = mainGames.Keys.Intersect(daddyGames.Keys).ToList();

        return new ComparisonReport
        {
            MainGames = mainGames.Count,
            DaddyGames = daddyGames.Count,
            MainMods = main?.LuaCount ?? 0,
            DaddyMods = daddy?.LuaCount ?? 0,
            MainManifests = main?.ManifestCount ?? 0,
            DaddyManifests = daddy?.ManifestCount ?? 0,
            MainSize = main?.GamesSizeBytes ?? 0,
            DaddySize = daddy?.GamesSizeBytes ?? 0,
            SharedGames = shared.Select(id => mainGames[id]).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
            MainOnly = mainGames.Where(kv => !daddyGames.ContainsKey(kv.Key)).Select(kv => kv.Value).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
            DaddyOnly = daddyGames.Where(kv => !mainGames.ContainsKey(kv.Key)).Select(kv => kv.Value).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    // ----------------------------------------------------------- connectivity

    public sealed class ConnectivityReport
    {
        public bool Dns { get; set; }
        public bool Internet { get; set; }
        public bool SteamStore { get; set; }
        public bool SteamCommunity { get; set; }
        public List<string> Details { get; set; } = new();
    }

    public static async Task<ConnectivityReport> CheckConnectivityAsync()
    {
        var report = new ConnectivityReport();

        try
        {
            var entry = await System.Net.Dns.GetHostEntryAsync("store.steampowered.com");
            report.Dns = entry.AddressList.Length > 0;
        }
        catch (Exception ex)
        {
            report.Details.Add($"DNS: {ex.Message}");
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("1.1.1.1", 2500);
            report.Internet = reply.Status == IPStatus.Success;
        }
        catch (Exception ex)
        {
            report.Details.Add($"Ping: {ex.Message}");
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamProfileSwitcher/2.0");

        async Task<bool> Probe(string url, string label)
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return (int)response.StatusCode < 500;
            }
            catch (Exception ex)
            {
                report.Details.Add($"{label}: {ex.Message}");
                return false;
            }
        }

        report.SteamStore = await Probe("https://store.steampowered.com", "Steam Store");
        report.SteamCommunity = await Probe("https://steamcommunity.com", "Steam Community");

        if (!report.Internet && (report.SteamStore || report.SteamCommunity))
            report.Internet = true;

        return report;
    }
}
