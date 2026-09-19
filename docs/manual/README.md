# AizenRex Steam Switcher - The Manual

Everything about this project, written so that someone who has never seen the
code can understand it, change it, and ship it.

| # | Document | Read it when |
|---|---|---|
| 00 | [Start here](00-START-HERE.md) | You want to know what this is, in plain language |
| 01 | [How it works](01-HOW-IT-WORKS.md) | You want to understand the switch and why it is safe |
| 02 | [The codebase](02-THE-CODEBASE.md) | You are opening the source for the first time |
| 03 | [How to modify](03-HOW-TO-MODIFY.md) | You want to add a page, a setting or a feature |
| 04 | [Build, test, release](04-BUILD-TEST-RELEASE.md) | You are shipping a version |
| 05 | [User guide](05-USER-GUIDE.md) | You are using the app and something is unclear |

---

## The one-paragraph version

Steam only ever looks for its installation at one fixed folder. AizenRex Steam
Switcher keeps two Steam installations - **Main** and **Daddy** - and swaps which
one sits at that path, so you can change which account Steam starts with by
clicking one button instead of logging out and back in. It backs up the config
files first, journals what it is about to do, and can repair itself if the swap
is interrupted by a crash or a power cut.

## The thirty-second orientation

| Question | Answer |
|---|---|
| What language? | C# on .NET 9, WPF with WPF-UI |
| Where does the version live? | `src/SteamSwitcher/Core/AppInfo.cs` |
| Where does it write? | Only next to its own executable |
| How does it update? | `Core/UpdateService.cs`, from this repo's GitHub releases |
| How do I test the risky part? | `dotnet run --project tools/vdftest/vdftest.csproj` |
| How do I release? | `git tag v2.2.0 && git push origin v2.2.0` |
| What must never be committed? | Real `.vdf` files and `build/backups/` |