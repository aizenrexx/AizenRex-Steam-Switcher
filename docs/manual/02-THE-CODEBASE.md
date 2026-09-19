# 02 - The codebase

## Layout

```
Steam Switcher\
  SteamSwitcher.sln              solution: app + test harness
  aizen_steam_installer.iss      Inno Setup installer script
  build.ps1 / cleanup.ps1        developer helpers
  license.txt  README.md  SECURITY.md  CONTRIBUTING.md

  .github\workflows\release.yml  the entire cloud release pipeline

  src\SteamSwitcher\
    SteamSwitcher.csproj
    App.xaml / App.xaml.cs        startup, global error handling
    MainWindow.xaml / .cs         the shell: nav, status bar, refresh loop
    app.manifest                  requireAdministrator

    Core\                         all logic that touches anything real
    Pages\                        one folder of views per screen
    Assets\                       icons

  tools\vdftest\                  the VDF test harness
  docs\                           architecture and release references
  docs\manual\                    this manual
```

## Core, file by file

| File | What it owns |
|---|---|
| `AppInfo.cs` | **Version, author, repository.** The single source of truth |
| `AppPaths.cs` | Every path the app uses, derived from the executable |
| `SwitchEngine.cs` | Reads status, plans, journals, switches, recovers |
| `VdfDocument.cs` | Parses VDF to a tree and writes it back |
| `LoginUsers.cs` | Reads accounts, sets the auto-login flag on exactly one |
| `SteamLocator.cs` | Finds Steam via registry, then common paths |
| `Store.cs` | Loads and saves settings, history, journal |
| `Settings.cs` | The settings model |
| `UpdateService.cs` | Self-update from this repo's GitHub releases |
| `SteamDaddyUpdater.cs` | Update for the external SteamDaddy tool |
| `Backups.cs` | Creating, listing and restoring config backups |
| `Log.cs` | Rolling log file |
| `Format.cs` | Human-readable sizes, dates, profile labels |
| `OpResult.cs` | The success/failure result type used across Core |

## Pages

Every page is a WPF `Page`. Pages that show live state implement
`IRefreshablePage`:

```csharp
public interface IRefreshablePage
{
    void OnStatusUpdated(SystemStatus status);
    void OnBusyChanged(bool busy);
}
```

`MainWindow` raises `StatusRefreshed` after each poll and pushes the new state
into whichever page is currently open, so a page never has to poll on its own.

Pages reach the shell through `MainWindow.Current` - WPF-UI's `NavigationView`
constructs pages itself, so they cannot be handed a constructor argument.

## The result type

Core never throws for expected failures. It returns:

```csharp
public sealed record OpResult(bool Success, string Message)
{
    public static OpResult Ok(string message) => new(true, message);
    public static OpResult Fail(string message) => new(false, message);
}
```

The caller decides whether to show a dialog, log, or both. Exceptions are
reserved for genuinely unexpected states and are caught at the top level in
`App.xaml.cs`.

## Conventions

| Area | Rule |
|---|---|
| Version | Only `AppInfo.CurrentVersion` - everything else reads it |
| File writes | Journal, back up, write, verify |
| Logging | `Info` for state changes, `Warn` recoverable, `Error` failures |
| UI text | Sentence case, no exclamation marks, no internal tool names |
| Naming | `_camelCase` private fields, `PascalCase` public members |
| Comments | Explain *why* the code is the way it is |

## The dependency surface

Deliberately tiny:

| Package | Why |
|---|---|
| `WPF-UI` 4.0.0 | The Fluent window, navigation and controls |

Everything else is the .NET 9 base class library. No JSON library beyond
`System.Text.Json`, no logging framework beyond `Log.cs`, no updater framework
beyond `UpdateService.cs`.

That is on purpose: this program renames folders in a protected system
directory, so every dependency is something that could break a user's Steam
install. Fewer is better.

## The project file

```xml
<TargetFramework>net9.0-windows</TargetFramework>
<OutputType>WinExe</OutputType>
<UseWPF>true</UseWPF>
<PlatformTarget>x64</PlatformTarget>
<PublishReadyToRun>true</PublishReadyToRun>
<TieredPGO>true</TieredPGO>
<InvariantGlobalization>false</InvariantGlobalization>
<Version>2.2.0</Version>
```

`InvariantGlobalization` must stay `false` - WPF uses culture-aware formatting,
and the invariant mode breaks it.

---

Next: [03 - How to modify](03-HOW-TO-MODIFY.md)