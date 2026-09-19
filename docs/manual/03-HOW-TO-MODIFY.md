# 03 - How to modify

## Add a page

1. Create `src/SteamSwitcher/Pages/YourPage.xaml` and `.xaml.cs`, both
   `partial`, the XAML root a `<Page>`.
2. Inherit `Page`. If it shows live state, also implement `IRefreshablePage`.
3. Register it in `MainWindow.xaml` inside the `NavigationView`:

```xml
<ui:NavigationViewItem Content="Your page"
                       TargetPageType="{x:Type pages:YourPage}" />
```

4. Model the structure on `DashboardPage` - it is the reference implementation.

```csharp
public partial class YourPage : Page, IRefreshablePage
{
    public YourPage() => InitializeComponent();

    public void OnStatusUpdated(SystemStatus status)
    {
        // called automatically whenever the shell refreshes
    }

    public void OnBusyChanged(bool busy) { }
}
```

## Add a setting

1. Add the property to `Core/Settings.cs` with a sensible default:

```csharp
public bool MyNewOption { get; set; } = true;
```

2. Add the control to `Pages/SettingsPage.xaml`, following the existing groups.
3. Wire it in `SettingsPage.xaml.cs` - load it in the load method, save it in the
   save handler.
4. If it changes runtime behaviour, call `MainWindow.Current.OnSettingsSaved()`.

Settings are saved as `data\settings.json`. Adding a property is safe: missing
keys fall back to the default, so existing installs keep working.

## Change the version

Edit **`src/SteamSwitcher/Core/AppInfo.cs`**:

```csharp
public const string CurrentVersion = "2.2.1";
```

Then update the four mirrors or the release pipeline will refuse to build:

| File | What |
|---|---|
| `src/SteamSwitcher/SteamSwitcher.csproj` | `<Version>` |
| `aizen_steam_installer.iss` | `#define MyAppVersion` |
| `license.txt` | the `Version:` line |
| `README.md` | the download table and badge |

## Touch the VDF logic

This is the risky part of the codebase. If you change `VdfDocument`,
`LoginUsers` or the switch steps in `SwitchEngine`:

1. **Extend `tools/vdftest/Program.cs` first.** Add a check that fails against
   the current code and passes against your fix.
2. Run the harness:

```powershell
dotnet run --project tools/vdftest/vdftest.csproj -c Release
```

3. Never test against the real `C:\Program Files (x86)\Steam\config\loginusers.vdf`.
   The harness copies it into a temp folder; do the same.

## Add an update source

`UpdateService` is specific to this repository. If you need a second source:

```csharp
var check = await UpdateService.CheckAsync();
if (check.HasUpdate) { /* check.LatestVersion, check.DownloadUrl */ }
```

Prefer extending `UpdateService` over adding a parallel implementation -
`SteamDaddyUpdater` already shows how much duplicated machinery that costs.

## Change the installer

`aizen_steam_installer.iss` is standard Inno Setup 6.

| Want to change | Edit |
|---|---|
| Install folder | `DefaultDirName` |
| Add a shortcut | the `[Icons]` section |
| Ship an extra file | the `[Files]` section |
| Post-install action | the `[Run]` section |
| Uninstall behaviour | the `[UninstallDelete]` section |

Two things to leave alone:

- **`AppId`** - changing it makes Windows treat the build as a different
  application, so upgrades install side by side instead of replacing.
- **The empty data paths in `[UninstallDelete]`** - an uninstall must never
  delete a user's backups.

Test a compile locally:

```powershell
$env:AIZEN_SOURCE_ROOT = (Get-Location).Path
$env:AIZEN_VERSION = "2.2.1"
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" aizen_steam_installer.iss
```

## Style rules that matter here

| Rule | Because |
|---|---|
| Journal before you write | A crash must always be recoverable |
| Back up before you touch config | The user's account is not yours to risk |
| Never regex-patch a VDF file | The original bug |
| Never write outside `AppPaths` | The app is portable by design |
| Fail loudly, never silently | A silent failure here loses someone's login |

---

Next: [04 - Build, test, release](04-BUILD-TEST-RELEASE.md)