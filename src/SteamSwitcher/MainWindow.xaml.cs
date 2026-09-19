using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SteamSwitcher.Core;
using Wpf.Ui.Appearance;
using NavigationView = Wpf.Ui.Controls.NavigationView;
using NavigatedEventArgs = Wpf.Ui.Controls.NavigatedEventArgs;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;

// MessageBox* names exist in both System.Windows and Wpf.Ui.Controls.
// The dialogs below are the classic Win32 ones, so bind them to System.Windows.
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace SteamSwitcher;

public partial class MainWindow
{
    private readonly DispatcherTimer _refreshTimer;

    private SystemStatus _status = new();
    private bool _busy;
    private bool _uiReady;

    /// <summary>
    /// The single live instance. WPF UI's NavigationView constructs pages
    /// itself from TargetPageType, so pages cannot be handed a constructor
    /// argument - they reach the shell through this instead.
    /// </summary>
    public static MainWindow? Current { get; private set; }

    /// <summary>
    /// Puts the build's version and date in the title bar. Without this there
    /// is no way to tell which build is running, which made it impossible to
    /// know whether a fix was actually live.
    /// </summary>
    private void StampVersionInTitle()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";
            if (exePath.Length == 0 || !System.IO.File.Exists(exePath)) return;

            // AppInfo owns the version. Reading it here means the title bar
            // can never disagree with what the project declares.
            var version = AppInfo.CurrentVersion;

            var built = new System.IO.FileInfo(exePath).LastWriteTime.ToString("MMM d, HH:mm");

            var title = $"{AppInfo.ProductName}  v{version}  ({built})";

            Title = title;
            if (AppTitleBar is not null) AppTitleBar.Title = title;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not stamp version in title: {ex.Message}");
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        StampVersionInTitle();
        Current = this;

        ApplicationThemeManager.Apply(this);

        Settings = Store.LoadSettings();
        Engine = new SwitchEngine(Settings);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(3, Settings.SafeRefreshSeconds))
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync(silent: true);

        Loaded += OnLoaded;
        Closed += (_, _) => Current = null;
    }

    // ------------------------------------------------------------- properties

    public Settings Settings { get; private set; }

    /// <summary>The name the pages use to reach the live settings.</summary>
    public Settings CurrentSettings => Settings;
    public SwitchEngine Engine { get; private set; }
    public SystemStatus CurrentStatus => _status;
    public bool IsBusy => _busy;

    /// <summary>Raised after every status refresh so open pages can update.</summary>
    public event Action<SystemStatus>? StatusRefreshed;

    /// <summary>Raised when a long operation starts or finishes.</summary>
    public event Action<bool>? BusyChanged;

    // ---------------------------------------------------------------- startup

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _uiReady = true;

        // A machine with no profiles set up yet goes straight to the wizard,
        // which can install Steam itself. Skipping the dashboard here avoids
        // greeting a fresh PC with "Folder not found" everywhere.
        if (FirstRunService.NeedsSetup(Settings))
        {
            Log.Info("No profiles configured - opening first-run setup.");
            RootNavigation.Navigate(typeof(Pages.SetupWizardPage));

            CheckElevation();
            return;
        }

        RootNavigation.Navigate(typeof(Pages.DashboardPage));

        await RefreshAsync();

        if (Settings.AutoRefresh) _refreshTimer.Start();

        CheckElevation();
        await CheckInterruptedSwitchAsync();
    }

    /// <summary>
    /// Called by the setup wizard once it finishes, so the shell starts
    /// behaving normally without needing a restart.
    /// </summary>
    public async Task ResumeAfterSetupAsync()
    {
        Settings = Store.LoadSettings();
        Engine = new SwitchEngine(Settings);

        RootNavigation.Navigate(typeof(Pages.DashboardPage));

        await RefreshAsync();

        if (Settings.AutoRefresh) _refreshTimer.Start();
        await CheckInterruptedSwitchAsync();
    }

    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        // Push the current status into a page the moment it appears, so a
        // freshly created page is never left showing empty values.
        if (_uiReady && args.Page is IRefreshablePage page)
            page.OnStatusUpdated(_status);
    }

    private void CheckElevation()
    {
        if (_status.IsElevated)
        {
            ElevationText.Text = "";
            return;
        }

        ElevationText.Text = "Not elevated - switching is disabled.";

        MessageBox.Show(
            "AizenRex Steam Switcher is not running as administrator.\n\n" +
            "Renaming folders inside C:\\Program Files (x86) needs elevation, so switching " +
            "profiles will fail until you restart the app with 'Run as administrator'.",
            "Administrator rights needed",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <summary>
    /// Detects a switch that never finished (crash, power loss, forced reboot)
    /// and offers the single safe repair.
    /// </summary>
    private async Task CheckInterruptedSwitchAsync()
    {
        var journal = Engine.PendingJournal();
        var plan = Engine.BuildRecoveryPlan(_status);

        if (journal is null && plan.Count == 0) return;

        var detail = journal is not null
            ? $"A switch from {Format.ProfileLabel(journal.From)} to {Format.ProfileLabel(journal.To)} " +
              $"started at {journal.StartedAt:yyyy-MM-dd HH:mm:ss} and never finished."
            : "The Steam folders are not in a normal layout.";

        if (plan.Count == 0)
        {
            Store.ClearJournal();
            MessageBox.Show(
                detail + "\n\nThis layout cannot be repaired automatically. " +
                "Open Diagnostics to see the current folder state.",
                "Previous switch did not finish",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(
            detail + "\n\nAizenRex Steam Switcher can restore " +
            $"'{System.IO.Path.GetFileName(plan[0].From)}' as the active Steam folder.\n\nRepair now?",
            "Previous switch did not finish",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        var result = Engine.Recover();
        SetStatus(result.Message);
        await RefreshAsync();

        MessageBox.Show(result.Message, result.Success ? "Repaired" : "Repair failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    // ------------------------------------------------------------------ refresh

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await RefreshAsync();

    public async Task RefreshAsync(bool silent = false)
    {
        if (_busy) return;

        try
        {
            if (!silent) SetBusy(true, "Reading Steam folders...");

            _status = await Task.Run(() => Engine.ReadStatus());

            UpdateChrome();
            StatusRefreshed?.Invoke(_status);

            LastRefreshText.Text = $"Updated {DateTime.Now:HH:mm:ss}";
            if (!silent) SetStatus("Ready");
        }
        catch (Exception ex)
        {
            Log.Error("Refresh failed", ex);
            SetStatus($"Refresh failed: {ex.Message}");
        }
        finally
        {
            if (!silent) SetBusy(false);
        }
    }

    private void UpdateChrome()
    {
        if (!_uiReady) return;

        ActiveProfileText.Text = _status.ActiveProfile switch
        {
            ProfileKind.Main => Settings.MainDisplayName,
            ProfileKind.Daddy => Settings.DaddyDisplayName,
            _ => "Unknown"
        };

        ActiveDot.Fill = _status.ActiveProfile switch
        {
            ProfileKind.Main => (Brush)FindResource("MainProfileBrush"),
            ProfileKind.Daddy => (Brush)FindResource("DaddyProfileBrush"),
            _ => (Brush)FindResource("TextFillColorTertiaryBrush")
        };

        SteamStateText.Text = _status.SteamRunning
            ? $"Steam running - {_status.RunningProcesses.Count}"
            : "Steam closed";

        SteamStatePill.Appearance = _status.SteamRunning
            ? ControlAppearance.Success
            : ControlAppearance.Secondary;

        if (!_status.IsElevated)
        {
            WarningPill.Visibility = Visibility.Visible;
            WarningPillText.Text = "Not administrator";
        }
        else if (_status.NeedsRecovery)
        {
            WarningPill.Visibility = Visibility.Visible;
            WarningPillText.Text = "Folder layout needs attention";
        }
        else
        {
            WarningPill.Visibility = Visibility.Collapsed;
        }

        if (_status.IsElevated) ElevationText.Text = "";
    }

    // ------------------------------------------------------------- busy / status

    public void SetStatus(string message)
    {
        if (!_uiReady) return;
        StatusText.Text = message;
        Log.Debug($"UI status: {message}");
    }

    public void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        if (!_uiReady) return;

        BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !busy;

        if (message is not null) StatusText.Text = message;
        else if (!busy) StatusText.Text = "Ready";

        BusyChanged?.Invoke(busy);
    }

    /// <summary>Applies a settings change from the Settings page.</summary>
    public void OnSettingsSaved()
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(3, Settings.SafeRefreshSeconds));
        if (Settings.AutoRefresh) _refreshTimer.Start();
    }

    /// <summary>Navigates from code, e.g. a dashboard button opening Diagnostics.</summary>
    public void NavigateTo(Type pageType) => RootNavigation.Navigate(pageType);
}

/// <summary>Implemented by every page that reacts to a status refresh.</summary>
public interface IRefreshablePage
{
    void OnStatusUpdated(SystemStatus status);
    void OnBusyChanged(bool busy);
}