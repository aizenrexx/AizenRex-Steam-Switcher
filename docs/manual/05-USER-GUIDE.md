# 05 - User guide

## Installing

Pick one:

**Installer** - run `AizenRex-Steam-Switcher-Setup-v2.2.0.exe`. It needs
administrator rights and creates a Start Menu entry.

**Portable** - unzip `AizenRex-Steam-Switcher-v2.2.0-Portable.zip` anywhere and
run `SteamSwitcher.exe`.

### First launch

Windows shows **"Windows protected your PC"**. This is SmartScreen, and it
appears for any new application that has not built download reputation yet.

1. **More info**
2. **Run anyway**

### Administrator rights

The app must run as administrator - it renames folders inside
`C:\Program Files (x86)`, which Windows blocks otherwise. If it launches without
elevation it says so in the status bar and disables switching.

To always launch elevated: right-click the shortcut -> **Properties** ->
**Advanced** -> tick **Run as administrator**.

## First-time setup

The wizard opens automatically when no profiles are configured.

1. **Main Steam folder** - detected from the registry. Accept it, or browse.
2. **Daddy Steam folder** - pick an existing second installation, or let the
   wizard create one by copying the Main install.
3. **Display names** - what each profile is called in the UI. Purely cosmetic.
4. **Finish** - the dashboard opens and the app behaves normally from then on.

## The pages

### Dashboard

The state of the world:

- Which profile is live
- Whether Steam is currently running
- Whether the folder layout is healthy
- The **Switch** button

### Library

Games installed in each profile. Useful for confirming that switching really
did change which installation Steam sees.

### Compare

The two profiles side by side - accounts, game counts, sizes.

### History

Every switch, newest first, with a timestamp and the result.

### Backups

Every config backup, with a **Restore** button. Backups are created
automatically before each switch and are never deleted by the app.

### Diagnostics

The current folder layout and any problem with it. This is the page to open
when something looks wrong.

### Tools

Maintenance actions - clearing old logs, re-reading Steam's folders, and the
SteamDaddy updater.

### Logs

The application log, newest last.

### Settings

Grouped into Appearance, Behaviour, Safety and About.

## Switching profiles

1. Make sure Steam is **closed** (the app refuses otherwise, unless you have
   turned the guard off)
2. Open **Dashboard**
3. Pick the profile you want
4. Click **Switch**

The app journals the intent, backs up the config, renames the folders, sets the
auto-login flag, verifies the result, and clears the journal. The status bar
narrates each step.

If you have **Auto-launch Steam** enabled, Steam starts when the switch
finishes.

## When something goes wrong

### "Not elevated - switching is disabled"

Close the app, right-click it, **Run as administrator**.

### "Steam is running"

Close Steam completely - check the system tray. The app will not swap folders
under a live Steam, because that is how installs get corrupted.

### "Previous switch did not finish"

The machine crashed or lost power mid-switch. The app offers to restore the
previous layout - click **Yes**. If it says it cannot repair automatically, open
**Diagnostics** and check the folder state.

### "Unknown account"

The account name in the config does not match either profile. Open the Setup
Wizard and re-select the Steam folders, or check **Diagnostics**.

### Steam opens with the wrong account

Open **Settings -> About** and note the version. Then open **Diagnostics** and
check that exactly one profile is live. If the auto-login flag looks wrong,
switch to the other profile and back - that rewrites it cleanly.

### The installer will not run

Some antivirus products block unsigned installers. The build is unsigned unless
a code-signing certificate is configured; the source is public and the pipeline
that produced it is in `.github/workflows/release.yml`.

## Updates

**Settings -> About -> Updates**:

1. **Check for updates** - asks GitHub for the newest release
2. If a newer one exists, the button becomes **Download and install**
3. The download is verified against the published SHA256
4. The installer runs, keeps your settings, history and backups, and the app
   closes so its files can be replaced

A portable-only release opens the releases page instead, since a portable
upgrade is a manual unzip.

## Where your data lives

Everything is inside the application folder:

| Folder | Contents |
|---|---|
| `data\` | Settings, history, journal |
| `logs\` | `steam-switcher.log` |
| `backups\` | Config backups, one folder each |
| `exports\` | Exports and downloaded updates |

Nothing is written to `%APPDATA%` or the registry. Copy the whole folder to
another machine and your settings come with it.

## Uninstalling

Use the uninstaller in the Start Menu. It removes the program but **leaves your
`backups\`, `logs\` and `data\` folders in place** - delete them yourself if you
want them gone.

Your Steam installations are never touched by an uninstall.

---

Back to [the manual index](README.md)