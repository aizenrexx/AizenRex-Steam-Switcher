namespace SteamSwitcher.Core;

/// <summary>
/// Single source of truth for the application's identity: version, author
/// credit, and the GitHub location updates are published to.
///
/// The version lives here and nowhere else in code. The window title, the
/// About tab and the update check all read <see cref="CurrentVersion"/> at
/// runtime, so a stale hard-coded string can never disagree with the build.
///
/// When releasing, bump this constant together with:
///   src/SteamSwitcher/SteamSwitcher.csproj  (&lt;Version&gt;)
///   aizen_steam_installer.iss               (MyAppVersion)
///   license.txt                             (Version: line)
///   README.md                               (badge)
/// The release pipeline fails the build when they disagree.
/// </summary>
public static class AppInfo
{
    /// <summary>Product name shown to the user.</summary>
    public const string ProductName = "AizenRex Steam Switcher";

    /// <summary>Shorter name used where space is tight.</summary>
    public const string ShortName = "Steam Switcher";

    /// <summary>The running application version. THE single source of truth.</summary>
    public const string CurrentVersion = "2.2.0";

    /// <summary>Author credit line, shown in the app, the installer and the repo.</summary>
    public const string Author = "Aizenrex x Riyad";

    /// <summary>Role line under the author name on the About tab.</summary>
    public const string AuthorRole = "Design | Engineering | Maintenance";

    /// <summary>GitHub account that owns the repository.</summary>
    public const string RepoOwner = "aizenrexx";

    /// <summary>Repository name.</summary>
    public const string RepoName = "AizenRex-Steam-Switcher";

    /// <summary>SPDX licence identifier.</summary>
    public const string LicenseName = "MIT";

    /// <summary>Human-readable build stamp, e.g. "v2.2.0".</summary>
    public static string VersionTag => "v" + CurrentVersion;

    /// <summary>e.g. "AizenRex Steam Switcher v2.2.0".</summary>
    public static string DisplayVersion => $"{ProductName} v{CurrentVersion}";

    public static string Copyright => $"Copyright (C) 2026 {Author}";

    public static string RepositoryUrl => $"https://github.com/{RepoOwner}/{RepoName}";
    public static string ReleasesUrl => $"{RepositoryUrl}/releases";
    public static string IssuesUrl => $"{RepositoryUrl}/issues";
    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
}