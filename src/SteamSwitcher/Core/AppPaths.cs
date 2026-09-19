using System.IO;
using System.Reflection;

namespace SteamSwitcher.Core;

/// <summary>
/// Every file this application creates lives inside the application folder.
/// Nothing is ever written to AppData, Documents, Temp or anywhere outside.
/// </summary>
public static class AppPaths
{
    /// <summary>Folder that contains SteamSwitcher.exe.</summary>
    public static string AppDir { get; }

    /// <summary>Settings, history, user data.  &lt;App&gt;\data</summary>
    public static string DataDir { get; }

    /// <summary>Rotating log files.  &lt;App&gt;\logs</summary>
    public static string LogDir { get; }

    /// <summary>Steam config backups.  &lt;App&gt;\backups</summary>
    public static string BackupDir { get; }

    /// <summary>Crash-recovery journal.  &lt;App&gt;\data\journal</summary>
    public static string JournalDir { get; }

    /// <summary>Exported reports (CSV / logs).  &lt;App&gt;\exports</summary>
    public static string ExportDir { get; }

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string HistoryFile => Path.Combine(DataDir, "history.json");
    public static string UserDataFile => Path.Combine(DataDir, "user-data.json");
    public static string JournalFile => Path.Combine(JournalDir, "switch-journal.json");
    public static string LogFile => Path.Combine(LogDir, "steam-switcher.log");

    static AppPaths()
    {
        // AppContext.BaseDirectory is the correct source in every hosting mode,
        // including single-file publish, where Assembly.Location returns "".
        // Environment.ProcessPath is the real exe path and is preferred when
        // available; BaseDirectory is the documented fallback.
        var processPath = Environment.ProcessPath;
        var dir = !string.IsNullOrWhiteSpace(processPath) && Path.IsPathRooted(processPath)
            ? Path.GetDirectoryName(processPath)
            : null;

        AppDir = dir is { Length: > 0 } && Directory.Exists(dir)
            ? dir
            : AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        DataDir = Path.Combine(AppDir, "data");
        LogDir = Path.Combine(AppDir, "logs");
        BackupDir = Path.Combine(AppDir, "backups");
        JournalDir = Path.Combine(DataDir, "journal");
        ExportDir = Path.Combine(AppDir, "exports");
    }

    /// <summary>Creates every application folder. Safe to call repeatedly.</summary>
    public static void EnsureAll()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(JournalDir);
        Directory.CreateDirectory(ExportDir);
    }

    /// <summary>
    /// True when <paramref name="path"/> sits inside the application folder.
    /// Used to guarantee no write escapes the project directory.
    /// </summary>
    public static bool IsInsideAppDir(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(AppDir).TrimEnd(Path.DirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
