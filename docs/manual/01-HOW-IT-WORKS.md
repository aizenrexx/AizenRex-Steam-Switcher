# 01 - How it works

## The folder swap

Steam resolves its installation from the path it was installed to, normally
`C:\Program Files (x86)\Steam`. Two installations therefore cannot both be at
that path at the same time - but they can *take turns*.

```
   BEFORE                                  AFTER
   Steam          -> MAIN account          Steam          -> DADDY account
   Steam - Daddy  -> DADDY account         Steam - Daddy  -> MAIN account
```

Only the folder names change. The contents - games, `steamapps`, config - stay
exactly where they are.

## Why the login flags matter

Steam decides which account to sign in automatically from
`config\loginusers.vdf`. It is a nested tree:

```
"users"
{
    "76561198000000001"
    {
        "AccountName"      "mainaccount"
        "PersonaName"      "Main"
        "MostRecent"       "1"
        "Timestamp"        "1700000000"
        "AllowAutoLogin"   "1"
    }
    "76561198000000002"
    {
        "AccountName"      "daddyaccount"
        "MostRecent"       "0"
        ...
    }
}
```

`MostRecent 1` marks the account Steam starts with. **Exactly one account may
have it.** Two accounts flagged `1` is the bug that produces the classic
"Steam logged into the wrong account" complaint, and it is precisely the bug
this project's test harness was written to catch.

## The nine steps of a switch

| # | Step | Why |
|---|---|---|
| 1 | **Read status** | Which folder is live, is Steam running, are we elevated |
| 2 | **Refuse if unsafe** | Steam running, or no admin rights -> stop with a reason |
| 3 | **Build the plan** | Which folder becomes which, and what the login flags should be |
| 4 | **Journal the intent** | Written to disk *before* anything changes |
| 5 | **Back up config** | `loginusers.vdf`, `config.vdf`, `localconfig.vdf` |
| 6 | **Rename the folders** | The actual swap |
| 7 | **Set the login flags** | Exactly one `MostRecent 1` |
| 8 | **Verify** | Re-read the disk and confirm the result |
| 9 | **Clear the journal** | Only now is the switch considered done |

Step 4 is the important one. Everything after it is recoverable, because the
app knows what it was doing.

## Recovery

On every launch the app looks for a journal file. If one is present, a switch
started and never finished - a crash, a power cut, a forced reboot.

It then:

1. Reads the current folder layout
2. Works out whether the pre-switch layout can be restored
3. Offers to restore it, in one click

If the layout is too damaged to repair automatically, it says so and points at
Diagnostics instead of guessing.

## Why VDF is parsed, not regex-patched

The original implementation used a regular expression to flip `MostRecent`. It
worked perfectly with one account and silently corrupted files with two,
because the pattern matched more than intended.

`VdfDocument` instead:

1. **Parses** the file into a tree of keys and values
2. **Changes** exactly the node it means to change
3. **Writes** the tree back in the same shape

The harness in `tools/vdftest` proves this against a *copy* of a real
`loginusers.vdf` - the original is never touched.

## The test harness

```
TEST 1  round-trip a real file (copy)          - parse, write, accounts survive
TEST 2  two accounts, exactly one MostRecent   - the original bug
TEST 3  missing AllowAutoLogin must be inserted
TEST 4  unknown account is rejected, file left alone
TEST 5  missing file handled, not crashed on
TEST 6  SteamID32 derived from SteamID64
TEST 7  Steam detection on this machine
```

Fifteen checks. Run it with:

```powershell
dotnet run --project tools/vdftest/vdftest.csproj -c Release
```

It runs in the release pipeline too. If it fails, the release does not happen.

## Where everything is written

Everything the app owns lives next to its executable:

```
<app folder>
  SteamSwitcher.exe
  data\            settings.json, history.json, user-data.json, journal\
  logs\            steam-switcher.log
  backups\         one folder per backup
  exports\         anything you export, plus downloaded updates
```

Nothing is written to `%APPDATA%`, the registry, or anywhere else. Move the
folder and the whole app moves with its settings.

The two exceptions are deliberate and are the entire point of the program: the
Steam folders you asked it to manage.

## Updates

Two separate updaters exist, and they are easy to confuse:

| Updater | Updates | Where it lives |
|---|---|---|
| `UpdateService` | **This app** | `Core/UpdateService.cs` |
| `SteamDaddyUpdater` | The external **SteamDaddy** tool | `Core/SteamDaddyUpdater.cs` |

`UpdateService` asks this repository's releases API for the newest tag, compares
it with `AppInfo.CurrentVersion`, and offers the installer when the tag is
newer. Downloads are verified against the published SHA256 before anything is
executed.

---

Next: [02 - The codebase](02-THE-CODEBASE.md)