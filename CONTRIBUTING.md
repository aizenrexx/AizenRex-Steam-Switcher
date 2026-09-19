# Contributing

Thanks for taking a look at AizenRex Steam Switcher.

## Ground rules

1. **Never commit real Steam data.** `loginusers.vdf`, `config.vdf`,
   `localconfig.vdf`, `*.cok` and anything under `build/backups/` belong to a
   real account. `.gitignore` blocks them - do not override it.
2. **Never commit a secret.** No tokens, certificates or `.env` files.
3. **Keep the credit.** The MIT licence requires the copyright notice to stay.

## Before you open a pull request

```powershell
dotnet build SteamSwitcher.sln -c Release
dotnet run --project tools/vdftest/vdftest.csproj -c Release
```

Both must be clean. The VDF harness is the safety net for the file-writing
logic - if you touch `VdfDocument`, `LoginUsers` or `SwitchEngine`, extend it.

## Conventions

| Area | Rule |
|---|---|
| Version | Bump `AppInfo.CurrentVersion` and all four mirrors, or CI fails |
| Logging | `Log.Info` for state changes, `Log.Warn` for recoverable, `Log.Error` for failures |
| File writes | Journal first, back up second, write third, verify fourth |
| UI text | Sentence case, no exclamation marks, no tool names |
| Comments | Explain *why*, not *what* |

## Adding a page

1. Add `Pages/YourPage.xaml` + `.xaml.cs`, inheriting `Page`.
2. Implement `IRefreshablePage` if it shows live status.
3. Register it in `MainWindow.xaml` under the `NavigationView`.
4. Follow the structure of `DashboardPage` - it is the reference.

## Reporting a bug

Open an issue with the app version (Settings -> About), what you did, what
happened, and the relevant lines from `logs\steam-switcher.log`.