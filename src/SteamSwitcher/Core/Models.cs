using System.Text.Json.Serialization;

namespace SteamSwitcher.Core;

/// <summary>Which Steam environment a folder represents.</summary>
public enum ProfileKind
{
    Unknown = 0,
    Main,   // clean / legitimate account
    Daddy   // modded: manifests, lua plugins, Millennium
}

/// <summary>A single installed Steam application parsed from appmanifest_*.acf.</summary>
public sealed class GameInfo
{
    public string AppId { get; set; } = "";
    public string Name { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool IsRedistributable { get; set; }

    public string SizeText => Format.HumanSize(SizeBytes);
}

/// <summary>Account details read from config/loginusers.vdf.</summary>
public sealed class SteamAccount
{
    public string SteamId64 { get; set; } = "";
    public long SteamId32 { get; set; }
    public string AccountName { get; set; } = "";
    public string PersonaName { get; set; } = "";

    public string Display => string.IsNullOrWhiteSpace(PersonaName) ? AccountName : PersonaName;
}

/// <summary>Everything discovered about one Steam directory.</summary>
public sealed class FolderReport
{
    public bool Exists { get; set; }
    public string Path { get; set; } = "";
    public ProfileKind Kind { get; set; } = ProfileKind.Unknown;

    public List<GameInfo> Games { get; set; } = new();
    public List<string> LuaFiles { get; set; } = new();
    public int ManifestCount { get; set; }
    public List<string> ManifestSample { get; set; } = new();

    public bool HasMillennium { get; set; }
    public bool HasSteamDaddy { get; set; }
    public SteamAccount? Account { get; set; }

    public int GameCount => Games.Count(g => !g.IsRedistributable);
    public int TotalAppCount => Games.Count;
    public int LuaCount => LuaFiles.Count;
    public long GamesSizeBytes => Games.Sum(g => g.SizeBytes);
}

/// <summary>A running Steam-related process.</summary>
public sealed class ProcessRow
{
    public string Name { get; set; } = "";
    public int Pid { get; set; }
    public long MemoryBytes { get; set; }
    public string MemoryText => Format.HumanSize(MemoryBytes);
}

/// <summary>Overall state of the machine: which profile is live, what is running.</summary>
public sealed class SystemStatus
{
    public ProfileKind ActiveProfile { get; set; } = ProfileKind.Unknown;

    public bool SteamFolderExists { get; set; }
    public bool MainFolderExists { get; set; }
    public bool DaddyFolderExists { get; set; }

    public bool SteamRunning { get; set; }
    public List<ProcessRow> RunningProcesses { get; set; } = new();

    public FolderReport? Active { get; set; }
    public FolderReport? Main { get; set; }
    public FolderReport? Daddy { get; set; }

    /// <summary>True when folders are in a state the app cannot interpret.</summary>
    public bool NeedsRecovery { get; set; }
    public string RecoveryHint { get; set; } = "";

    public bool IsElevated { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
}

/// <summary>Result of any operation shown to the user.</summary>
public sealed class OpResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";

    public static OpResult Ok(string message) => new() { Success = true, Message = message };
    public static OpResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>One row in the switch history.</summary>
public sealed class HistoryEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    [JsonIgnore]
    public string TimeText => Time.ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string StatusText => Success ? "Success" : "Failed";
}

/// <summary>A single diagnostic check.</summary>
public sealed class CheckRow
{
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public CheckLevel Level { get; set; } = CheckLevel.Pass;

    public bool Ok => Level == CheckLevel.Pass;
}

public enum CheckLevel { Pass, Warn, Fail }

/// <summary>Stored config backup.</summary>
public sealed class BackupEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime Created { get; set; }
    public string Profile { get; set; } = "";
    public List<string> Items { get; set; } = new();
    public long SizeBytes { get; set; }

    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm:ss");
    public string SizeText => Format.HumanSize(SizeBytes);
    public string ItemsText => Items.Count == 0 ? "—" : string.Join(", ", Items);
}

/// <summary>Journal record written before a switch so a crash can be recovered.</summary>
public sealed class SwitchJournal
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public ProfileKind From { get; set; }
    public ProfileKind To { get; set; }
    public List<JournalStep> Steps { get; set; } = new();
    public bool Completed { get; set; }
}

public sealed class JournalStep
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public bool Done { get; set; }
}
