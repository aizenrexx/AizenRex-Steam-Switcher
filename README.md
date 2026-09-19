<div align="center">

# AizenRex Steam Switcher

**Two Steam installations. One click to swap between them.**

A Windows desktop tool that keeps a *Main* Steam and a *Daddy* Steam side by side
and switches which one is active - safely, with backups, with a full audit trail,
and without ever losing a login.

[![Release](https://img.shields.io/github/v/release/aizenrexx/AizenRex-Steam-Switcher?style=for-the-badge&label=release&color=5B8CFF)](https://github.com/aizenrexx/AizenRex-Steam-Switcher/releases)
[![Build](https://img.shields.io/github/actions/workflow/status/aizenrexx/AizenRex-Steam-Switcher/release.yml?style=for-the-badge&label=build)](https://github.com/aizenrexx/AizenRex-Steam-Switcher/actions)
[![Downloads](https://img.shields.io/github/downloads/aizenrexx/AizenRex-Steam-Switcher/total?style=for-the-badge&color=2ea043)](https://github.com/aizenrexx/AizenRex-Steam-Switcher/releases)
[![Licence](https://img.shields.io/badge/licence-MIT-blue?style=for-the-badge)](license.txt)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6?style=for-the-badge&logo=windows&logoColor=white)](#requirements)

**Built by Aizenrex x Riyad**

[Download](#download) &nbsp;|&nbsp; [How it works](#how-it-works) &nbsp;|&nbsp; [Features](#features) &nbsp;|&nbsp; [Documentation](#documentation) &nbsp;|&nbsp; [Building](#building-from-source)

</div>

---

## What is this?

Steam only supports one active installation at a time. If you share a PC, run two
accounts, or want a separate install for a second person, the usual answer is
"log out, log back in, hope nothing breaks".

**AizenRex Steam Switcher** makes that a single button. It renames the two Steam
folders so the one you want sits at the standard Steam path, writes the matching
auto-login entry, and takes a backup before it touches anything.

If a switch is ever interrupted - power cut, crash, forced reboot - the app
detects it on the next launch and offers to repair the folder layout.

> **Nothing is written outside the application folder.** Settings, history,
> backups and exports all live next to the executable, so the whole tool is
> portable by design.

---

## Download

| Package | What it is | Use it when |
|---|---|---|
| **`AizenRex-Steam-Switcher-Setup-v2.2.0.exe`** | Installer (Inno Setup) | You want a normal Windows install with a Start Menu entry |
| **`AizenRex-Steam-Switcher-v2.2.0-Portable.zip`** | Portable build | You want to unzip and run, or carry it on a USB stick |

Both are on the [**Releases page**](https://github.com/aizenrexx/AizenRex-Steam-Switcher/releases/latest).

### First launch

Windows will show **"Windows protected your PC"** (SmartScreen). That is expected
for any new unsigned application.

1. Click **More info**
2. Click **Run anyway**

> The app needs **administrator rights** - it renames folders inside
> `C:\Program Files (x86)`, which Windows does not allow otherwise. Right-click
> the shortcut and choose *Run as administrator* if you ever launch it manually.

---

## Features

<table>
<tr><td width="50%" valign="top">

### Switching
- **One-click swap** between Main and Daddy Steam
- **Automatic backup** before every switch
- **Interrupted-switch detection** with one-click repair
- **Auto-login handling** - the right account ends up flagged
- **Steam running guard** - refuses to swap under a live Steam
- **Optional Steam relaunch** after the swap

</td><td width="50%" valign="top">

### Visibility
- **Dashboard** with live folder and account state
- **Library** view of installed games per profile
- **Compare** the two profiles side by side
- **History** of every switch, with timestamps
- **Backups** browser with restore
- **Diagnostics** for the current folder layout
- **Logs** view with the full app log

</td></tr>
<tr><td valign="top">

### Safety
- Every write is **journaled** before it happens
- **VDF files are parsed and rebuilt**, never regex-patched
- Unknown accounts are **rejected**, not guessed at
- Backups are **kept, never auto-deleted**
- **Nothing outside the app folder** is touched

</td><td valign="top">

### Quality of life
- **Built-in self-update** - checks GitHub releases in-app
- **SteamDaddy updater** - keeps the companion tool current
- **First-run setup wizard** - can find or install Steam for you
- **Light and dark themes** with an accent colour
- **Credit screen** showing version, build and author

</td></tr>
</table>

---

## How it works

Steam always looks for its installation at one fixed path. The trick is that the
*contents* of that path can be swapped.

```
        BEFORE A SWITCH                    AFTER A SWITCH
   ---------------------------       ---------------------------
   Steam         -> Main account     Steam         -> Daddy account
   Steam - Daddy -> Daddy account    Steam - Daddy -> Main account
```

The app:

1. **Reads** the current folder layout and the accounts in `loginusers.vdf`
2. **Journals** the intended change, so an interruption is always recoverable
3. **Backs up** the config files it is about to touch
4. **Renames** the two Steam folders
5. **Rewrites** the auto-login flags so exactly one account is `MostRecent`
6. **Verifies** the result and clears the journal

Because the rename is the only destructive-looking step and it is journaled
first, the app can always tell you what it was in the middle of doing.

> Detailed walkthrough: [`docs/manual/01-HOW-IT-WORKS.md`](docs/manual/01-HOW-IT-WORKS.md)

---

## Requirements

| | |
|---|---|
| **OS** | Windows 10 or 11, 64-bit |
| **Runtime** | None - the build is self-contained |
| **Rights** | Administrator (required for the folder rename) |
| **Disk** | ~130 MB for the portable build, ~42 MB for the installer |
| **Steam** | Any recent version, installed to the default location |

---

## Documentation

The full manual lives in the repository:

| Document | Covers |
|---|---|
| [**00 - Start here**](docs/manual/00-START-HERE.md) | What the project is, in plain language |
| [**01 - How it works**](docs/manual/01-HOW-IT-WORKS.md) | The switch, step by step, and why it is safe |
| [**02 - The codebase**](docs/manual/02-THE-CODEBASE.md) | Every folder and file, explained |
| [**03 - How to modify**](docs/manual/03-HOW-TO-MODIFY.md) | Add a page, a setting, a feature |
| [**04 - Build, test, release**](docs/manual/04-BUILD-TEST-RELEASE.md) | From `git clone` to a published release |
| [**05 - User guide**](docs/manual/05-USER-GUIDE.md) | Using the app day to day |

Plus:

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) - component map
- [`docs/BUILD-AND-RELEASE.md`](docs/BUILD-AND-RELEASE.md) - pipeline reference
- [`docs/SECURITY-AND-SECRETS.md`](docs/SECURITY-AND-SECRETS.md) - what is never committed
- [`SECURITY.md`](SECURITY.md) - reporting a vulnerability
- [`CONTRIBUTING.md`](CONTRIBUTING.md) - conventions

---

## Building from source

```powershell
git clone https://github.com/aizenrexx/AizenRex-Steam-Switcher.git
cd AizenRex-Steam-Switcher

# Build
dotnet build SteamSwitcher.sln -c Release

# Run the VDF test harness (15 checks, no Steam data is modified)
dotnet run --project tools/vdftest/vdftest.csproj -c Release

# Portable build
dotnet publish src/SteamSwitcher/SteamSwitcher.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o Distribution/Portable

# Installer (needs Inno Setup 6)
ISCC.exe aizen_steam_installer.iss
```

Requires the **.NET 9 SDK**. See
[`docs/manual/04-BUILD-TEST-RELEASE.md`](docs/manual/04-BUILD-TEST-RELEASE.md)
for the long version.

---

## Releasing

Releases are built entirely in the cloud - no local machine involved.

```powershell
git tag v2.2.0
git push origin v2.2.0
```

That triggers [`.github/workflows/release.yml`](.github/workflows/release.yml), which:

1. Builds and tests on `windows-latest`
2. **Verifies the version matches** in `AppInfo.cs`, the `.csproj`, the installer script and the licence
3. Runs the VDF harness
4. Publishes the portable build and zips it
5. Compiles the Inno Setup installer
6. Signs both, if a certificate is configured
7. Creates the GitHub release with both files attached

> The version check exists so an installed copy can never be told "you are up to
> date" while a newer build sits on the releases page.

### Where the version lives

`src/SteamSwitcher/Core/AppInfo.cs` is the **single source of truth**:

```csharp
public const string CurrentVersion = "2.2.0";
```

Everything else reads it at runtime. When you release, bump it together with the
four mirrors the pipeline checks (`.csproj`, `.iss`, `license.txt`, and the badge
above) - or the build fails on purpose.

---

## Updates

The app updates itself from this repository's releases. **Settings -> About ->
Updates** shows the installed version, checks GitHub, and installs a newer
release in place.

- The installer route **preserves your settings, history and backups**
- A portable-only release opens the releases page instead
- Downloads are **verified against the published SHA256** before anything runs
- A build newer than the published release is never "downgraded"

---

## Safety notes

- **Your Steam account data never leaves your PC.** Nothing is uploaded anywhere.
- **Backups are never deleted automatically.** They live in the app's `backups`
  folder and stay until you remove them.
- **The app refuses to switch while Steam is running**, unless you turn that
  guard off in Settings.
- **A failed switch is recoverable.** The journal survives a crash and the app
  offers the repair on next launch.

---

## Credits

<table>
<tr>
<td><b>Author</b></td><td>Aizenrex x Riyad</td>
</tr>
<tr>
<td><b>Role</b></td><td>Design | Engineering | Maintenance</td>
</tr>
<tr>
<td><b>Licence</b></td><td>MIT - see <a href="license.txt">license.txt</a></td>
</tr>
</table>

If you use, modify or redistribute this project, **keep the credit in place**.

---

## FAQ

<details>
<summary><b>Will this get me banned from Steam?</b></summary>

No. It does not modify Steam, inject into it, or touch the game files. It renames
two folders on your own disk and writes the standard auto-login flag that Steam
itself uses. Valve's rules cover cheating and account sharing in the sense of
letting others play on your account - this is a local folder manager.

</details>

<details>
<summary><b>What happens to my games?</b></summary>

Nothing. Both installations keep their own `steamapps` folder. Switching changes
which one Steam sees at the standard path - the games themselves are untouched
and stay where they are.

</details>

<details>
<summary><b>Can I lose my login?</b></summary>

The app backs up `loginusers.vdf`, `config.vdf` and `localconfig.vdf` before every
switch, and the VDF writer only ever changes the auto-login flags. Even if a
switch is interrupted mid-way, the journal lets the app restore the previous
layout.

</details>

<details>
<summary><b>Why does it need administrator rights?</b></summary>

`C:\Program Files (x86)` is a protected location. Renaming a folder inside it
requires elevation. There is no way around that without moving Steam, which would
break other things.

</details>

<details>
<summary><b>Why does SmartScreen warn me?</b></summary>

The installer is not signed with a paid code-signing certificate, so Windows has
no reputation for it yet. *More info -> Run anyway* is the normal path. The
pipeline will sign automatically the moment a certificate is configured.

</details>

<details>
<summary><b>Does it work with a non-default Steam location?</b></summary>

Yes. The Setup Wizard detects Steam from the registry and lets you point at a
custom install. Both profiles can live anywhere.

</details>

---

<div align="center">

**AizenRex Steam Switcher** - built and maintained by **Aizenrex x Riyad**

[Releases](https://github.com/aizenrexx/AizenRex-Steam-Switcher/releases) &nbsp;Â·&nbsp;
[Issues](https://github.com/aizenrexx/AizenRex-Steam-Switcher/issues) &nbsp;Â·&nbsp;
[MIT Licence](license.txt)

</div>