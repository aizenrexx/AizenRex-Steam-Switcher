using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SteamSwitcher.Core;

/// <summary>
/// A notice published by the SteamDaddy team in their release notes.
/// </summary>
public sealed class NoticeInfo
{
    public string Version { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>
    /// True when the notice states something the user MUST do, rather than
    /// general information. Those are shown prominently.
    /// </summary>
    public bool IsMandatory { get; init; }

    public bool HasContent => Text.Trim().Length > 0;
}

/// <summary>
/// Reads the release notes from the configured update source and pulls out
/// anything the SteamDaddy team says is required, so the instruction is visible
/// inside this app instead of only on the GitHub page.
///
/// Everything read here is untrusted text from a third party: it is displayed
/// to the user as a quoted notice and is never executed or acted on.
/// </summary>
public static partial class UpstreamNotice
{
    /// <summary>Phrases that mark an instruction as required rather than optional.</summary>
    private static readonly string[] MandatoryMarkers =
    {
        "must", "required", "mandatory", "you have to", "make sure",
        "do not", "don't", "important", "warning", "note:", "admin",
        "administrator", "run as", "necessary", "needed", "before you"
    };

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineSplit();

    /// <summary>Strips the common Markdown decorations so the text reads cleanly in WPF.</summary>
    [GeneratedRegex(@"[*_`#>]+")]
    private static partial Regex MarkdownNoise();

    /// <summary>
    /// Fetches the latest release notes and extracts the notice lines.
    /// Returns null when nothing relevant is published, or on any failure:
    /// a missing notice must never block a switch.
    /// </summary>
    public static async Task<NoticeInfo?> FetchAsync(
        string apiUrl, CancellationToken cancellation = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamSwitcher/2.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(apiUrl, cancellation);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            return Extract(tag, body);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the upstream notice: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Pulls the instruction lines out of a release-notes body.
    /// Kept separate from the network call so it can be tested directly.
    /// </summary>
    public static NoticeInfo? Extract(string version, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        var keep = new List<string>();
        var mandatory = false;

        foreach (var raw in LineSplit().Split(body))
        {
            var line = MarkdownNoise().Replace(raw, "").Trim();

            // Drop bullet markers and empty or decorative lines.
            line = line.TrimStart('-', '•', '*', ' ').Trim();
            if (line.Length < 8) continue;
            if (line.All(c => c is '=' or '-' or '_')) continue;

            var lower = line.ToLowerInvariant();
            var hit = MandatoryMarkers.Any(marker => lower.Contains(marker));
            if (!hit) continue;

            mandatory = true;
            if (!keep.Contains(line)) keep.Add(line);

            // A handful of lines is enough; the full notes stay on GitHub.
            if (keep.Count >= 6) break;
        }

        if (keep.Count == 0) return null;

        return new NoticeInfo
        {
            Version = version,
            Text = string.Join("\n\n", keep.Select(l => "• " + l)),
            IsMandatory = mandatory
        };
    }
}
