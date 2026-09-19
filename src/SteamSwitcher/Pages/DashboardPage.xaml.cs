using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class DashboardPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;
    private SystemStatus _status = new();

    private sealed record Metric(string Label, string Value, string Note);

    public DashboardPage()
    {
        InitializeComponent();

        // The navigation host caches pages and only pushes status on a
        // Navigated event, so a page sitting in the background never saw a
        // refresh that happened while it was visible. Subscribing to the
        // shell's own events makes the dashboard update the instant a switch
        // or a refresh completes, with no tab change needed.
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var shell = MainWindow.Current;
        if (shell is null) return;

        shell.StatusRefreshed += OnShellStatusRefreshed;
        shell.BusyChanged += OnShellBusyChanged;

        // Paint immediately with whatever the shell already knows.
        OnStatusUpdated(shell.CurrentStatus);
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        var shell = MainWindow.Current;
        if (shell is null) return;

        shell.StatusRefreshed -= OnShellStatusRefreshed;
        shell.BusyChanged -= OnShellBusyChanged;
    }

    /// <summary>Marshals onto the UI thread, then repaints.</summary>
    private void OnShellStatusRefreshed(SystemStatus status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnStatusUpdated(status));
            return;
        }

        OnStatusUpdated(status);
    }

    private void OnShellBusyChanged(bool busy)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnBusyChanged(busy));
            return;
        }

        OnBusyChanged(busy);
    }

    // ------------------------------------------------------------------ refresh

    public void OnStatusUpdated(SystemStatus status)
    {
        _status = status;
        var settings = _shell.CurrentSettings;

        MainNameText.Text = settings.MainDisplayName;
        DaddyNameText.Text = settings.DaddyDisplayName;

        SubtitleText.Text = status.ActiveProfile switch
        {
            ProfileKind.Main => $"{settings.MainDisplayName} is active. Switch to {settings.DaddyDisplayName} when you want the modded setup.",
            ProfileKind.Daddy => $"{settings.DaddyDisplayName} is active. Switch back to {settings.MainDisplayName} for the clean account.",
            _ => "The active profile could not be identified. Check Diagnostics."
        };

        // ---- recovery banner
        if (status.NeedsRecovery)
        {
            RecoveryBanner.Visibility = Visibility.Visible;
            RecoveryText.Text = status.RecoveryHint;
            RepairButton.IsEnabled = _shell.Engine.BuildRecoveryPlan(status).Count == 1 && status.IsElevated;
        }
        else
        {
            RecoveryBanner.Visibility = Visibility.Collapsed;
        }

        // ---- profile cards
        FillCard(status.Main, status.ActiveProfile == ProfileKind.Main,
            MainAccountText, MainGamesText, MainSizeText, MainModsText, MainActivePill);

        FillCard(status.Daddy, status.ActiveProfile == ProfileKind.Daddy,
            DaddyAccountText, DaddyGamesText, DaddySizeText, DaddyModsText, DaddyActivePill);

        // ---- switch buttons
        var mainIsActive = status.ActiveProfile == ProfileKind.Main;
        var daddyIsActive = status.ActiveProfile == ProfileKind.Daddy;

        var mainAvailable = status.MainFolderExists || mainIsActive;
        var daddyAvailable = status.DaddyFolderExists || daddyIsActive;

        SwitchToMainButton.IsEnabled = !mainIsActive && mainAvailable && status.IsElevated && !_shell.IsBusy;
        SwitchToDaddyButton.IsEnabled = !daddyIsActive && daddyAvailable && status.IsElevated && !_shell.IsBusy;

        MainButtonText.Text = mainIsActive
            ? "Already active"
            : mainAvailable ? $"Switch to {settings.MainDisplayName}" : "Profile folder missing";

        DaddyButtonText.Text = daddyIsActive
            ? "Already active"
            : daddyAvailable ? $"Switch to {settings.DaddyDisplayName}" : "Profile folder missing";

        // ---- metrics
        var totalGames = (status.Main?.GameCount ?? 0) + (status.Daddy?.GameCount ?? 0);
        var totalSize = (status.Main?.GamesSizeBytes ?? 0) + (status.Daddy?.GamesSizeBytes ?? 0);
        var history = Store.LoadHistory();
        var lastSwitch = history.FirstOrDefault(h => h.Action.StartsWith("Switch", StringComparison.OrdinalIgnoreCase));

        MetricsHost.ItemsSource = new List<Metric>
        {
            new("TOTAL GAMES", totalGames.ToString(), "across both profiles"),
            new("TOTAL SIZE", Format.HumanSize(totalSize), "installed game data"),
            new("LUA MODS", ((status.Main?.LuaCount ?? 0) + (status.Daddy?.LuaCount ?? 0)).ToString(), "stplug-in scripts"),
            new("MANIFESTS", ((status.Main?.ManifestCount ?? 0) + (status.Daddy?.ManifestCount ?? 0)).ToString(), "depotcache entries"),
            new("LAST SWITCH", lastSwitch is null ? "never" : Format.Relative(lastSwitch.Time),
                lastSwitch is null ? "no switch recorded" : lastSwitch.StatusText)
        };

        // ---- processes
        if (status.RunningProcesses.Count > 0)
        {
            ProcessGrid.Visibility = Visibility.Visible;
            ProcessGrid.ItemsSource = status.RunningProcesses;
            NoProcessText.Visibility = Visibility.Collapsed;
            CloseSteamButton.IsEnabled = !_shell.IsBusy;
        }
        else
        {
            ProcessGrid.Visibility = Visibility.Collapsed;
            NoProcessText.Visibility = Visibility.Visible;
            CloseSteamButton.IsEnabled = false;
        }
    }

    private void FillCard(
        FolderReport? report, bool isActive,
        TextBlock account, TextBlock games, TextBlock size, TextBlock mods, FrameworkElement activePill)
    {
        activePill.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        if (report is null || !report.Exists)
        {
            account.Text = "Folder not found";
            games.Text = "—";
            size.Text = "—";
            mods.Text = "—";
            return;
        }

        account.Text = report.Account is { } acc && acc.Display.Length > 0
            ? $"{acc.Display} · {acc.SteamId64}"
            : "No stored account";

        games.Text = report.GameCount.ToString();
        size.Text = Format.HumanSize(report.GamesSizeBytes);
        mods.Text = report.LuaCount.ToString();
    }

    public void OnBusyChanged(bool busy)
    {
        SwitchToMainButton.IsEnabled = !busy && SwitchToMainButton.IsEnabled;
        SwitchToDaddyButton.IsEnabled = !busy && SwitchToDaddyButton.IsEnabled;
        RepairButton.IsEnabled = !busy && RepairButton.IsEnabled;
        CloseSteamButton.IsEnabled = !busy && _status.RunningProcesses.Count > 0;

        if (!busy) OnStatusUpdated(_status);
    }

    // ------------------------------------------------------------------ actions

    private async void OnSwitchToMainClick(object sender, RoutedEventArgs e) =>
        await SwitchAsync(ProfileKind.Main);

    private async void OnSwitchToDaddyClick(object sender, RoutedEventArgs e) =>
        await SwitchAsync(ProfileKind.Daddy);

    private async Task SwitchAsync(ProfileKind target)
    {
        var settings = _shell.CurrentSettings;
        var label = target == ProfileKind.Main ? settings.MainDisplayName : settings.DaddyDisplayName;

        // Show what will happen, including any blocking check, before touching the disk.
        var checks = _shell.Engine.PreflightChecks(target, _status);
        var blocking = checks.Where(c => c.Level == CheckLevel.Fail).ToList();

        if (blocking.Count > 0)
        {
            MessageBox.Show(
                "This switch cannot run yet:\n\n" +
                string.Join("\n", blocking.Select(c => $"• {c.Name}: {c.Detail}")),
                "Switch blocked",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (settings.ConfirmSwitch)
        {
            var warnings = checks.Where(c => c.Level == CheckLevel.Warn).ToList();

            var message =
                $"Switch to {label}?\n\n" +
                "Steps:\n" +
                "  1. Close Steam\n" +
                (settings.BackupBeforeSwitch ? "  2. Back up the current profile config\n" : "") +
                $"  {(settings.BackupBeforeSwitch ? 3 : 2)}. Park the active profile and activate {label}\n" +
                (settings.SyncAutoLogin ? $"  {(settings.BackupBeforeSwitch ? 4 : 3)}. Sync the sign-in account\n" : "") +
                (settings.AutoLaunchSteam ? $"  {(settings.BackupBeforeSwitch ? 5 : 4)}. Start Steam\n" : "");

            if (warnings.Count > 0)
                message += "\nWarnings:\n" + string.Join("\n", warnings.Select(w => $"• {w.Name}: {w.Detail}"));

            if (_status.SteamRunning)
                message += "\n\nSteam is running and will be closed.";

            var answer = MessageBox.Show(message, "Confirm switch", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        _shell.SetBusy(true, $"Switching to {label}…");

        var progress = new Progress<string>(text => _shell.SetStatus(text));
        var result = await _shell.Engine.SwitchAsync(target, progress);

        _shell.SetBusy(false, result.Message);
        await _shell.RefreshAsync();

        MessageBox.Show(
            result.Message,
            result.Success ? "Switch complete" : "Switch failed",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private async void OnRepairClick(object sender, RoutedEventArgs e)
    {
        var plan = _shell.Engine.BuildRecoveryPlan(_status);
        if (plan.Count != 1)
        {
            MessageBox.Show(
                "This folder layout has no single safe repair. Open Diagnostics to review the state.",
                "Repair not available", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(
            $"Restore '{System.IO.Path.GetFileName(plan[0].From)}' as the active Steam folder?",
            "Repair folder layout", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        _shell.SetBusy(true, "Repairing folder layout…");
        var result = await Task.Run(() => _shell.Engine.Recover());
        _shell.SetBusy(false, result.Message);
        await _shell.RefreshAsync();

        MessageBox.Show(result.Message, result.Success ? "Repaired" : "Repair failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private async void OnCloseSteamClick(object sender, RoutedEventArgs e)
    {
        _shell.SetBusy(true, "Closing Steam…");
        var progress = new Progress<string>(text => _shell.SetStatus(text));
        var result = await SteamProcesses.CloseSteamAsync(progress: progress);
        _shell.SetBusy(false, result.Message);
        await _shell.RefreshAsync();
    }
}
