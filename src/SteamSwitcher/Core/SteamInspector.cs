using System.IO;
using System.Text.RegularExpressions;

namespace SteamSwitcher.Core;

/// <summary>
/// Reads a Steam directory: which kind it is, which account it holds,
/// which games are installed, how many lua mods and manifests exist.
/// Read-only: this class never modifies anything on disk.
/// </summary>
public static partial class SteamInspector
{
    private const long SteamId64Base = 76561197960265728L;

    [GeneratedRegex(@"""(\d{17})""\s*\{([^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex UserBlockRegex();

    [GeneratedRegex(@"""AccountName""\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AccountNameRegex();

    [GeneratedRegex(@"""PersonaName""\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex PersonaNameRegex();

    [GeneratedRegex(@"""MostRecent""\s*""1""", RegexOptions.IgnoreCase)]
    private static partial Regex MostRecentRegex();

    [GeneratedRegex(@"""appid""\s*""(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex AppIdRegex();

    [GeneratedRegex(@"""name""\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"""installdir""\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();

    [GeneratedRegex(@"""SizeOnDisk""\s*""(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex SizeRegex();

    /// <summary>
    /// Decides whether a folder is the modded (Daddy) or clean (Main) environment.
    /// Returns Unknown when the folder does not exist at all.
    /// </summary>
    public static ProfileKind DetectKind(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return ProfileKind.Unknown;

        try
        {
            if (Directory.Exists(Path.Combine(folder, "steamdaddy"))) return ProfileKind.Daddy;
            if (Directory.Exists(Path.Combine(folder, "millennium"))) return ProfileKind.Daddy;
            if (File.Exists(Path.Combine(folder, "cloud_redirect.dll"))) return ProfileKind.Daddy;

            var stplug = Path.Combine(folder, "config", "stplug-in");
            if (Directory.Exists(stplug) &&
                Directory.EnumerateFiles(stplug, "*.lua", SearchOption.AllDirectories).Any())
                return ProfileKind.Daddy;
        }
        catch (Exception ex)
        {
            Log.Warn($"Kind detection failed for {folder}: {ex.Message}");
        }

        return ProfileKind.Main;
    }

    /// <summary>Reads the signed-in account from config/loginusers.vdf.</summary>
    public static SteamAccount? ReadAccount(string folder)
    {
        var vdf = Path.Combine(folder, "config", "loginusers.vdf");
        if (!File.Exists(vdf)) return null;

        try
        {
            var content = File.ReadAllText(vdf);
            var blocks = UserBlockRegex().Matches(content);
            if (blocks.Count == 0) return null;

            // Prefer the account flagged MostRecent; fall back to the first entry.
            Match chosen = blocks[0];
            foreach (Match m in blocks)
            {
                if (MostRecentRegex().IsMatch(m.Groups[2].Value))
                {
                    chosen = m;
                    break;
                }
            }

            var id64Text = chosen.Groups[1].Value;
            var inner = chosen.Groups[2].Value;

            var account = AccountNameRegex().Match(inner) is { Success: true } a ? a.Groups[1].Value : "";
            var persona = PersonaNameRegex().Match(inner) is { Success: true } p ? p.Groups[1].Value : account;

            long id32 = 0;
            if (long.TryParse(id64Text, out var id64)) id32 = id64 - SteamId64Base;

            return new SteamAccount
            {
                SteamId64 = id64Text,
                SteamId32 = id32,
                AccountName = account,
                PersonaName = persona
            };
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read loginusers.vdf in {folder}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Parses one appmanifest_*.acf file.</summary>
    public static GameInfo? ParseAcf(string file)
    {
        try
        {
            var content = File.ReadAllText(file);

            var appId = AppIdRegex().Match(content) is { Success: true } m ? m.Groups[1].Value : "";
            if (appId.Length == 0) return null;

            var name = NameRegex().Match(content) is { Success: true } n && n.Groups[1].Value.Length > 0
                ? n.Groups[1].Value
                : $"App {appId}";

            var installDir = InstallDirRegex().Match(content) is { Success: true } d ? d.Groups[1].Value : "";
            long size = SizeRegex().Match(content) is { Success: true } s && long.TryParse(s.Groups[1].Value, out var v) ? v : 0;

            return new GameInfo
            {
                AppId = appId,
                Name = name,
                InstallDir = installDir,
                SizeBytes = size,
                IsRedistributable = appId == "228980"
            };
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not parse {Path.GetFileName(file)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Full read-only scan of one Steam folder.</summary>
    public static FolderReport Inspect(string folder)
    {
        var report = new FolderReport { Path = folder ?? "" };

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return report;

        report.Exists = true;
        report.Kind = DetectKind(folder);
        report.Account = ReadAccount(folder);

        // Installed apps
        var steamapps = Path.Combine(folder, "steamapps");
        if (Directory.Exists(steamapps))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
                {
                    var game = ParseAcf(file);
                    if (game is not null) report.Games.Add(game);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"steamapps scan failed in {folder}: {ex.Message}");
            }
        }

        report.Games = report.Games
            .OrderBy(g => g.IsRedistributable)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Lua plugins
        var stplug = Path.Combine(folder, "config", "stplug-in");
        if (Directory.Exists(stplug))
        {
            try
            {
                report.LuaFiles = Directory
                    .EnumerateFiles(stplug, "*.lua", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(stplug, f).Replace('\\', '/'))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warn($"lua scan failed in {folder}: {ex.Message}");
            }
        }

        // Depot manifests
        var depot = Path.Combine(folder, "depotcache");
        if (Directory.Exists(depot))
        {
            try
            {
                var manifests = Directory.EnumerateFiles(depot, "*.manifest").ToList();
                report.ManifestCount = manifests.Count;
                report.ManifestSample = manifests.Take(50).Select(Path.GetFileName).Where(n => n is not null).Select(n => n!).ToList();
            }
            catch (Exception ex)
            {
                Log.Warn($"manifest scan failed in {folder}: {ex.Message}");
            }
        }

        report.HasMillennium = Directory.Exists(Path.Combine(folder, "millennium"));
        report.HasSteamDaddy = Directory.Exists(Path.Combine(folder, "steamdaddy"))
                               || File.Exists(Path.Combine(folder, "cloud_redirect.dll"));

        return report;
    }

    /// <summary>Total size of a folder, bounded so a huge tree cannot hang the UI.</summary>
    public static long FolderSize(string folder, int maxFiles = 60_000)
    {
        if (!Directory.Exists(folder)) return 0;

        long total = 0;
        var count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (++count > maxFiles) break;
                try { total += new FileInfo(file).Length; } catch { /* skip locked files */ }
            }
        }
        catch { /* access denied on a subtree is fine */ }

        return total;
    }
}
