# Build and release

## Local build

```powershell
dotnet build SteamSwitcher.sln -c Release
dotnet run --project tools/vdftest/vdftest.csproj -c Release
```

Requires the .NET 9 SDK.

## Local packaging

```powershell
# Portable
dotnet publish src/SteamSwitcher/SteamSwitcher.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=2.2.0 -o Distribution/Portable

Compress-Archive -Path "Distribution/Portable/*" `
  -DestinationPath "Distribution/AizenRex-Steam-Switcher-v2.2.0-Portable.zip" -Force

# Installer (needs Inno Setup 6)
$env:AIZEN_SOURCE_ROOT = (Get-Location).Path
$env:AIZEN_VERSION = "2.2.0"
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" aizen_steam_installer.iss
```

Output:

| File | Where |
|---|---|
| `SteamSwitcher.exe` | `Distribution/Portable/` |
| `AizenRex-Steam-Switcher-v2.2.0-Portable.zip` | `Distribution/` |
| `AizenRex-Steam-Switcher-Setup-v2.2.0.exe` | `Distribution/Installer/` |

## Cloud release

```powershell
git tag v2.2.0
git push origin v2.2.0
```

`.github/workflows/release.yml` then runs on `windows-latest`:

| Step | What it does |
|---|---|
| Resolve version | From the tag, or the manual input |
| Build | `dotnet build` the solution |
| **Version gate** | Fails unless `AppInfo.cs`, `.csproj`, `.iss` and `license.txt` all agree |
| VDF harness | 15 checks against a copy of the real file |
| Publish | Self-contained single-file `win-x64` |
| ZIP | Portable archive |
| Inno Setup | Installs via Chocolatey, compiles the installer |
| Sign | Only when `WINDOWS_CERT_PFX_BASE64` and `WINDOWS_CERT_PASSWORD` secrets exist |
| Release | Creates the GitHub release and uploads both assets |

### Why the version gate exists

An installed copy asks the releases API for the newest tag and compares it with
`AppInfo.CurrentVersion`. If the tag says `v2.2.0` but the build still declares
`2.1.0`, every user is told they are up to date while a newer build sits on the
releases page. The gate makes that impossible to ship.

### Optional signing

Set two repository secrets:

| Secret | Value |
|---|---|
| `WINDOWS_CERT_PFX_BASE64` | Base64 of your `.pfx` |
| `WINDOWS_CERT_PASSWORD` | Its password |

Without them the workflow prints a notice and publishes unsigned. SmartScreen
will warn until the download builds reputation.

## Version mirrors

When you bump the version, change **all five**:

1. `src/SteamSwitcher/Core/AppInfo.cs` - `CurrentVersion` (the source of truth)
2. `src/SteamSwitcher/SteamSwitcher.csproj` - `<Version>`
3. `aizen_steam_installer.iss` - `#define MyAppVersion`
4. `license.txt` - the `Version:` line
5. `README.md` - the download table and badge