using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamSwitcher.Core;

/// <summary>User-editable configuration, persisted to &lt;App&gt;\data\settings.json.</summary>
public sealed class Settings
{
    // --- Paths -------------------------------------------------------------
    public string BaseDir { get; set; } = @"C:\Program Files (x86)";
    public string SteamDir { get; set; } = @"C:\Program Files (x86)\Steam";
    public string MainDir { get; set; } = @"C:\Program Files (x86)\Steam Main";
    public string DaddyDir { get; set; } = @"C:\Program Files (x86)\Steam Daddy";

    // --- Behaviour ---------------------------------------------------------
    public bool AutoRefresh { get; set; } = true;
    public int RefreshSeconds { get; set; } = 10;
    public bool ConfirmSwitch { get; set; } = true;
    public bool AutoLaunchSteam { get; set; } = true;
    public bool BackupBeforeSwitch { get; set; } = true;
    public bool SyncAutoLogin { get; set; } = true;

    // --- Appearance --------------------------------------------------------
    /// <summary>"Dark", "Light" or "System".</summary>
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#5B8CFF";

    // --- Profile customisation --------------------------------------------
    public string MainDisplayName { get; set; } = "Steam Main";
    public string DaddyDisplayName { get; set; } = "Steam Daddy";
    public string MainAccent { get; set; } = "#3FB950";
    public string DaddyAccent { get; set; } = "#A371F7";

    // --- Safety ------------------------------------------------------------
    /// <summary>Optional SteamID64 lock per profile; blocks a switch on mismatch.</summary>
    public string MainExpectedSteamId { get; set; } = "";
    public string DaddyExpectedSteamId { get; set; } = "";

    public bool Notifications { get; set; } = true;

    // --- SteamDaddy auto-update -------------------------------------------
    /// <summary>
    /// Check for a newer SteamDaddy release when switching to the Daddy
    /// profile, and install it automatically when one exists.
    /// </summary>
    public bool AutoUpdateSteamDaddy { get; set; } = true;

    /// <summary>
    /// Where SteamDaddy.exe lives. Empty means: use the location of the last
    /// install, falling back to the Desktop, which is where the official
    /// installer places it.
    /// </summary>
    public string SteamDaddyExePath { get; set; } = "";

    /// <summary>
    /// Where to look for SteamDaddy releases. Defaults to the official repo.
    /// If upstream ever moves, paste the new link once and it is saved here
    /// and reused for every later check.
    /// </summary>
    public string SteamDaddyUpdateUrl { get; set; } = DefaultSteamDaddyUpdateUrl;

    /// <summary>The built-in source, used when the custom link is cleared.</summary>
    public const string DefaultSteamDaddyUpdateUrl =
        "https://api.github.com/repos/Contrary7/SteamDaddy-Backup/releases/latest";

    /// <summary>
    /// Start Steam through a registered scheduled task so Windows does not
    /// show a UAC prompt on every switch. Set up once, then silent.
    /// </summary>
    public bool SilentElevation { get; set; } = true;

    [JsonIgnore]
    public string EffectiveSteamDaddyUpdateUrl =>
        string.IsNullOrWhiteSpace(SteamDaddyUpdateUrl)
            ? DefaultSteamDaddyUpdateUrl
            : SteamDaddyUpdateUrl.Trim();

    [JsonIgnore]
    public int SafeRefreshSeconds => Math.Clamp(RefreshSeconds, 3, 300);
}

/// <summary>Loads and saves settings, history and per-user data. All inside the app folder.</summary>
public static class Store
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object Gate = new();

    // ---------------------------------------------------------------- settings

    public static Settings LoadSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize<Settings>(json, Options);
                if (loaded is not null)
                {
                    loaded.RefreshSeconds = loaded.SafeRefreshSeconds;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"settings.json unreadable, using defaults: {ex.Message}");
        }

        return new Settings();
    }

    public static void SaveSettings(Settings settings)
    {
        settings.RefreshSeconds = settings.SafeRefreshSeconds;
        WriteAtomic(AppPaths.SettingsFile, settings);
    }

    // ----------------------------------------------------------------- history

    public static List<HistoryEntry> LoadHistory()
    {
        try
        {
            if (File.Exists(AppPaths.HistoryFile))
            {
                var json = File.ReadAllText(AppPaths.HistoryFile);
                return JsonSerializer.Deserialize<List<HistoryEntry>>(json, Options) ?? new();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"history.json unreadable: {ex.Message}");
        }
        return new List<HistoryEntry>();
    }

    public static void AddHistory(string action, bool success, string message)
    {
        try
        {
            var rows = LoadHistory();
            rows.Insert(0, new HistoryEntry
            {
                Time = DateTime.Now,
                Action = action,
                Success = success,
                Message = message
            });
            if (rows.Count > 500) rows = rows.Take(500).ToList();
            WriteAtomic(AppPaths.HistoryFile, rows);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not append history: {ex.Message}");
        }
    }

    public static void ClearHistory() => WriteAtomic(AppPaths.HistoryFile, new List<HistoryEntry>());

    // --------------------------------------------------------------- user data

    public sealed class UserData
    {
        public List<string> FavoriteAppIds { get; set; } = new();
        public Dictionary<string, string> LaunchArgs { get; set; } = new();
        public Dictionary<string, string> Notes { get; set; } = new()
        {
            ["main"] = "",
            ["daddy"] = ""
        };
        public List<RecentGame> RecentGames { get; set; } = new();
    }

    public sealed class RecentGame
    {
        public string AppId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Profile { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;

        [JsonIgnore] public string TimeText => Time.ToString("yyyy-MM-dd HH:mm");
    }

    public static UserData LoadUserData()
    {
        try
        {
            if (File.Exists(AppPaths.UserDataFile))
            {
                var json = File.ReadAllText(AppPaths.UserDataFile);
                return JsonSerializer.Deserialize<UserData>(json, Options) ?? new();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"user-data.json unreadable: {ex.Message}");
        }
        return new UserData();
    }

    public static void SaveUserData(UserData data) => WriteAtomic(AppPaths.UserDataFile, data);

    // ----------------------------------------------------------------- journal

    public static void WriteJournal(SwitchJournal journal) => WriteAtomic(AppPaths.JournalFile, journal);

    public static SwitchJournal? ReadJournal()
    {
        try
        {
            if (!File.Exists(AppPaths.JournalFile)) return null;
            var json = File.ReadAllText(AppPaths.JournalFile);
            return JsonSerializer.Deserialize<SwitchJournal>(json, Options);
        }
        catch (Exception ex)
        {
            Log.Warn($"journal unreadable: {ex.Message}");
            return null;
        }
    }

    public static void ClearJournal()
    {
        try
        {
            if (File.Exists(AppPaths.JournalFile)) File.Delete(AppPaths.JournalFile);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not clear journal: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ helper

    /// <summary>
    /// Writes JSON through a temp file then replaces the target, so a crash
    /// mid-write can never leave a half-written config behind.
    /// </summary>
    private static void WriteAtomic<T>(string path, T value)
    {
        if (!AppPaths.IsInsideAppDir(path))
        {
            Log.Error($"Refusing to write outside the application folder: {path}");
            return;
        }

        try
        {
            lock (Gate)
            {
                AppPaths.EnsureAll();
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));

                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed writing {Path.GetFileName(path)}", ex);
        }
    }
}
