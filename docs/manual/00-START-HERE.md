# 00 - Start here

## The problem

Steam is built around one installation in one place. On a shared PC - two
people, two accounts, or one person with a main and a secondary account - that
means logging out and back in every time you want to switch. It is slow, it
loses the "remember me" state, and if you do it while Steam is confused about
which folder is which, you can end up with a broken install.

## The idea

A Steam installation is just a folder. Steam always looks for it at the same
path, but nothing stops you from having a *second* folder sitting next to it and
renaming the two so the one you want is at the expected path.

```
   Main Steam   C:\Program Files (x86)\Steam
   Daddy Steam  C:\Program Files (x86)\Steam - Daddy
```

To switch to Daddy, you swap the names. That is the whole trick.

## What this app adds

Doing that by hand is a bad idea. You have to:

- Make sure Steam is not running
- Rename two folders in a protected directory
- Rewrite the auto-login flags in `loginusers.vdf` so the right account is the
  one Steam starts with
- Back up the config first, in case something goes wrong
- Remember what you were doing if the machine dies halfway

AizenRex Steam Switcher does all of it, and remembers enough to undo it.

## What you actually see

Ten pages, each answering one question:

| Page | Answers |
|---|---|
| **Dashboard** | Which profile is live right now? |
| **Library** | What games does each profile have? |
| **Compare** | How do the two profiles differ? |
| **History** | What switches have happened, and when? |
| **Backups** | What can I restore? |
| **Diagnostics** | Is the folder layout healthy? |
| **Tools** | Maintenance actions |
| **Logs** | What did the app do? |
| **Settings** | How should it behave? |
| **Setup Wizard** | Help me configure this for the first time |

Plus an **About** tab in Settings that shows the version, the build date, the
credit, and a **Check for updates** button.

## What it will never do

- Send anything anywhere. No telemetry, no accounts, no cloud.
- Touch your game files.
- Delete a backup on its own.
- Change anything outside its own folder and the two Steam folders you told it
  about.

## The five-minute tour

1. **Install** - run the setup `.exe`, or unzip the portable build.
2. **Run as administrator** - it renames folders in `Program Files`.
3. **Setup Wizard** - point it at your Main Steam folder, then at (or create) the
   second one.
4. **Dashboard** - shows which profile is active and whether Steam is running.
5. **Switch** - pick the other profile and click the button.

If anything looks wrong afterwards, **Diagnostics** tells you the current folder
state, and the app offers to repair an interrupted switch on the next launch.

---

Next: [01 - How it works](01-HOW-IT-WORKS.md)