# 04 - Build, test, release

## Requirements

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 9.0 | The only hard requirement |
| Inno Setup | 6 | Only to build the installer locally |
| Git | any recent | |

## Build

```powershell
git clone https://github.com/aizenrexx/AizenRex-Steam-Switcher.git
cd AizenRex-Steam-Switcher
dotnet build SteamSwitcher.sln -c Release
```

Expect `0 Warning(s), 0 Error(s)`.

## Test

```powershell
dotnet run --project tools/vdftest/vdftest.csproj -c Release
```

Fifteen checks over the VDF logic. It copies a real `loginusers.vdf` into a temp
folder - **your actual Steam config is never modified**.

```
TEST 1  round-trip the real file (copy)
TEST 2  two accounts, exactly one MostRecent   <- the bug this exists to catch
TEST 3  missing AllowAutoLogin must be inserted
TEST 4  unknown account is rejected, file left alone
TEST 5  missing file handled, not crashed on
TEST 6  SteamID32 derived from SteamID64
TEST 7  Steam detection on this machine

==== 15 passed, 0 failed ====
```

## Run

```powershell
dotnet run --project src/SteamSwitcher/SteamSwitcher.csproj -c Release
```

Run your terminal as administrator, or the folder rename will fail.

## Package locally

### Portable

```powershell
dotnet publish src/SteamSwitcher/SteamSwitcher.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=2.2.0 `
  -o Distribution/Portable

Compress-Archive -Path "Distribution/Portable/*" `
  -DestinationPath "Distribution/AizenRex-Steam-Switcher-v2.2.0-Portable.zip" -Force
```

### Installer

```powershell
$env:AIZEN_SOURCE_ROOT = (Get-Location).Path
$env:AIZEN_VERSION     = "2.2.0"
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" aizen_steam_installer.iss
```

### Output

| File | Location | Approx size |
|---|---|---|
| `SteamSwitcher.exe` | `Distribution/Portable/` | 126 MB |
| `AizenRex-Steam-Switcher-v2.2.0-Portable.zip` | `Distribution/` | 54 MB |
| `AizenRex-Steam-Switcher-Setup-v2.2.0.exe` | `Distribution/Installer/` | 42 MB |

The executable is large because it is self-contained: no .NET runtime needed on
the target machine. The installer is much smaller because Inno Setup compresses
it with LZMA2.

## Release

```powershell
git tag v2.2.0
git push origin v2.2.0
```

`.github/workflows/release.yml` takes it from there, on `windows-latest`:

| Step | What it does | Fails when |
|---|---|---|
| Resolve version | Reads the tag, or the manual input | The tag is not `vX.Y.Z` |
| Setup .NET 9 | | |
| Build | `dotnet build` the solution | Compile errors |
| **Version gate** | Compares `AppInfo.cs`, `.csproj`, `.iss`, `license.txt` | Any of them disagrees |
| VDF harness | 15 checks | Any check fails |
| Publish | Self-contained single-file `win-x64` | |
| ZIP | Portable archive | |
| Inno Setup | `choco install innosetup`, then compiles | The script has an error |
| Sign | Only when the certificate secrets exist | |
| Release | Creates the release, uploads both assets | An asset is missing |

### Why the version gate exists

An installed copy asks the releases API for the newest tag and compares it with
`AppInfo.CurrentVersion`. If the tag says `v2.2.0` while the build still
declares `2.1.0`, every user is told *"you are up to date"* while a newer build
sits on the releases page - and they never receive it.

The gate makes that impossible to publish.

### Optional signing

Add two repository secrets:

| Secret | Value |
|---|---|
| `WINDOWS_CERT_PFX_BASE64` | Base64 of your code-signing `.pfx` |
| `WINDOWS_CERT_PASSWORD` | Its password |

Without them the workflow prints a notice and publishes unsigned, which means
SmartScreen warns on first run until the download builds reputation.

## Version checklist

Before tagging, confirm **all five** agree:

- [ ] `src/SteamSwitcher/Core/AppInfo.cs` - `CurrentVersion`
- [ ] `src/SteamSwitcher/SteamSwitcher.csproj` - `<Version>`
- [ ] `aizen_steam_installer.iss` - `#define MyAppVersion`
- [ ] `license.txt` - the `Version:` line
- [ ] `README.md` - download table and badge

## Verifying a release

1. Download the installer from the releases page
2. Install it
3. Open **Settings -> About** - the version should match the tag
4. Click **Check for updates** - it should say you are on the newest version

That last step is the real test: it proves the installed build's declared
version and the published tag agree.

---

Next: [05 - User guide](05-USER-GUIDE.md)