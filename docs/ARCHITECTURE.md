# Architecture

## Layers

```
  UI           Pages/*.xaml(.cs)      MainWindow
   |             views, no file logic
   v
  Core         SwitchEngine           the only thing that mutates Steam
   |           UpdateService          self-update
   |           SteamDaddyUpdater      companion-tool update
   v
  IO           VdfDocument            parse and rebuild VDF
   |           LoginUsers             account flags
   |           Store / AppPaths       settings, history, journal, backups
   v
  Disk         Steam folders, app data folder
```

Nothing above `Core` writes to Steam. Nothing outside `AppPaths` writes to disk.

## Key types

| Type | Responsibility |
|---|---|
| `AppInfo` | Version, author, repository. **Single source of truth for the version** |
| `AppPaths` | Every path the app uses, all under the executable |
| `SwitchEngine` | Reads status, plans a switch, journals it, performs it, recovers |
| `VdfDocument` | Parses VDF into a tree and writes it back, byte-faithfully |
| `LoginUsers` | Reads accounts and sets the auto-login flag on exactly one |
| `SteamLocator` | Finds Steam via registry, then common paths |
| `Store` | Loads and saves `settings.json`, `history.json`, `journal` |
| `UpdateService` | Checks GitHub releases, verifies SHA256, runs the installer |
| `SteamDaddyUpdater` | Same for the external SteamDaddy tool, on a 6-hour throttle |
| `Log` | Rolling log in `logs\steam-switcher.log` |
| `IRefreshablePage` | Contract for pages that react to a status refresh |

## The switch, in order

1. `ReadStatus()` - what is active now, is Steam running, is elevation present
2. `BuildPlan()` - which folder becomes which
3. `Journal.Write()` - the intent hits disk **before** anything changes
4. `Backup()` - copy `loginusers.vdf`, `config.vdf`, `localconfig.vdf`
5. `Rename()` - the actual folder swap
6. `LoginUsers.SetActiveAccount()` - exactly one `MostRecent=1`
7. `Verify()` - re-read and confirm
8. `Journal.Clear()`

A crash anywhere after step 3 leaves a journal entry, which is what the
next launch detects and offers to repair.

## Why VDF is parsed, not regex-patched

Steam's config files are nested key/value trees. A regex that "works" on one
account silently corrupts a file with two - which is exactly the bug the harness
in `tools/vdftest` was written to catch. `VdfDocument` loads the tree, changes
the node it means to change, and writes the tree back.

## Update flow

```
Settings -> About -> Check for updates
        |
        v
UpdateService.CheckAsync()      GET /repos/.../releases/latest
        |                       compare tag with AppInfo.CurrentVersion
        v
   newer? -> prefer *-Setup-v*.exe, else *Portable*.zip
        |
        v
UpdateService.DownloadAsync()   download, SHA256 verify
        |
        v
UpdateService.LaunchInstaller() /SILENT /CLOSEAPPLICATIONS, then app exits
```

The installer keeps the user's data folder, so settings, history and backups
survive an upgrade.