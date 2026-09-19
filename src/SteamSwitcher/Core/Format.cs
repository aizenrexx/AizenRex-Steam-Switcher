namespace SteamSwitcher.Core;

public static class Format
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    /// <summary>Human readable byte size, e.g. "1.4 GB".</summary>
    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "0 B";

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{(long)value} {Units[unit]}"
            : $"{value:0.#} {Units[unit]}";
    }

    /// <summary>Friendly label for a profile kind.</summary>
    public static string ProfileLabel(ProfileKind kind) => kind switch
    {
        ProfileKind.Main => "Main",
        ProfileKind.Daddy => "Daddy",
        _ => "Unknown"
    };

    /// <summary>"2 minutes ago" style relative time.</summary>
    public static string Relative(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalSeconds < 45) return "just now";
        if (span.TotalMinutes < 2) return "1 minute ago";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 2) return "1 hour ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 2) return "yesterday";
        return $"{(int)span.TotalDays} days ago";
    }
}
