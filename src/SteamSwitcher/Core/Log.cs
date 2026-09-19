using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace SteamSwitcher.Core;

public enum LogLevel { Debug, Info, Warn, Error }

/// <summary>
/// Small thread-safe file logger with size-based rotation.
/// The old Python build grew a single 1.77 MB log with no rotation; this caps
/// the active file at 1 MB and keeps at most 3 archives (4 MB worst case).
/// </summary>
public static class Log
{
    private const long MaxBytes = 1_000_000;   // rotate at ~1 MB
    private const int MaxArchives = 3;         // .1 .2 .3

    private static readonly object Gate = new();
    private static readonly ConcurrentQueue<string> Recent = new();
    private const int RecentCapacity = 400;

    public static event Action<string>? LineWritten;

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    public static void Error(string message, Exception ex) =>
        Write(LogLevel.Error,
            $"{message} :: {ex.GetType().Name}: {ex.Message}" +
            Environment.NewLine + Describe(ex));

    /// <summary>
    /// Full detail for a logged exception: stack trace plus any inner
    /// exceptions. Without this a NullReferenceException says nothing about
    /// where it came from, which makes a crash report useless.
    /// </summary>
    private static string Describe(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        var depth = 0;

        while (current is not null && depth < 5)
        {
            sb.AppendLine($"    [{depth}] {current.GetType().FullName}: {current.Message}");

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                foreach (var frame in current.StackTrace.Split('\n'))
                    sb.AppendLine("        " + frame.TrimEnd('\r').Trim());
            }

            current = current.InnerException;
            depth++;
        }

        return sb.ToString().TrimEnd();
    }

    public static void Write(LogLevel level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {level.ToString().ToUpperInvariant(),-5} | {message}";

        Recent.Enqueue(line);
        while (Recent.Count > RecentCapacity) Recent.TryDequeue(out _);

        try
        {
            lock (Gate)
            {
                AppPaths.EnsureAll();
                RotateIfNeeded();
                File.AppendAllText(AppPaths.LogFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }

        try { LineWritten?.Invoke(line); } catch { /* ignore UI handler faults */ }
    }

    private static void RotateIfNeeded()
    {
        var path = AppPaths.LogFile;
        if (!File.Exists(path)) return;

        var info = new FileInfo(path);
        if (info.Length < MaxBytes) return;

        // Drop the oldest archive, shift the rest up, then move the active file.
        var oldest = $"{path}.{MaxArchives}";
        if (File.Exists(oldest)) File.Delete(oldest);

        for (var i = MaxArchives - 1; i >= 1; i--)
        {
            var src = $"{path}.{i}";
            var dst = $"{path}.{i + 1}";
            if (File.Exists(src)) File.Move(src, dst, overwrite: true);
        }

        File.Move(path, $"{path}.1", overwrite: true);
    }

    /// <summary>In-memory tail of the current session, newest last.</summary>
    public static IReadOnlyList<string> RecentLines() => Recent.ToArray();

    /// <summary>Reads the log file from disk, optionally filtered.</summary>
    public static List<string> ReadFile(string? query = null, LogLevel? level = null, int limit = 800)
    {
        try
        {
            if (!File.Exists(AppPaths.LogFile)) return new List<string>();

            var lines = File.ReadLines(AppPaths.LogFile).AsEnumerable();

            if (level is { } lv)
            {
                var token = $"| {lv.ToString().ToUpperInvariant()}";
                lines = lines.Where(l => l.Contains(token, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
                lines = lines.Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase));

            var all = lines.ToList();
            return all.Count <= limit ? all : all.GetRange(all.Count - limit, limit);
        }
        catch (Exception ex)
        {
            return new List<string> { $"(log read failed: {ex.Message})" };
        }
    }

    public static long CurrentSizeBytes()
    {
        try { return File.Exists(AppPaths.LogFile) ? new FileInfo(AppPaths.LogFile).Length : 0; }
        catch { return 0; }
    }

    public static void Clear()
    {
        try
        {
            lock (Gate)
            {
                if (File.Exists(AppPaths.LogFile)) File.WriteAllText(AppPaths.LogFile, string.Empty);
                for (var i = 1; i <= MaxArchives; i++)
                {
                    var archive = $"{AppPaths.LogFile}.{i}";
                    if (File.Exists(archive)) File.Delete(archive);
                }
            }
            while (Recent.TryDequeue(out _)) { }
        }
        catch (Exception ex)
        {
            Warn($"Could not clear logs: {ex.Message}");
        }
    }
}
