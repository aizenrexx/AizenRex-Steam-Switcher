using System.IO;
using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class SettingsPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;
    private SystemStatus _status = new();

    /// <summary>
    /// False while the controls are being populated, so programmatic writes
    /// are not mistaken for edits by the user.
    /// </summary>
    private bool _loaded;

    /// <summary>Accent options offered on the Appearance card.</summary>
    private static readonly (string Name, string Hex)[] AccentOptions =
    {
        ("Blue",   "#5B8CFF"),
        ("Green",  "#3FB950"),
        ("Purple", "#A371F7"),
        ("Amber",  "#D29922"),
        ("Rose",   "#F85149"),
        ("Teal",   "#2DD4BF"),
    };

    public SettingsPage()
    {
        InitializeComponent();
        BuildAccentSwatches();
        LoadFromSettings();
        LoadAboutInfo();
        LoadCreditInfo();

        Loaded += async (_, _) =>
        {
            _loaded = true;
            Anim.StaggerChildren(RootStack, stepMs: 50);

            // Fetched after the page is visible so it never delays opening.
            await ShowUpstreamNoticeAsync(_shell.CurrentSettings.EffectiveSteamDaddyUpdateUrl);
        };
    }

    /// <summary>Builds the accent colour row.</summary>
    private void BuildAccentSwatches()
    {
        foreach (var (name, hex) in AccentOptions)
        {
            var colour = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

            var swatch = new System.Windows.Controls.Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = name,
                Background = new System.Windows.Media.SolidColorBrush(colour),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Tag = hex
            };

            swatch.MouseEnter += (s, _) => Anim.Pulse((System.Windows.Controls.Border)s!, 1.12, 220);
            swatch.MouseLeftButtonUp += OnAccentPicked;

            AccentSwatches.Items.Add(swatch);
        }
    }

    private void OnAccentPicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border picked) return;

        foreach (var item in AccentSwatches.Items)
            if (item is System.Windows.Controls.Border b)
                b.BorderBrush = System.Windows.Media.Brushes.Transparent;

        picked.BorderBrush = System.Windows.Media.Brushes.White;
        Anim.Pulse(picked, 1.18, 300);

        _pendingAccent = picked.Tag as string ?? "";
        MarkDirty();
    }

    private string _pendingAccent = "";

    // -------------------------------------------------------------------- load

    private void LoadFromSettings()
    {
        var settings = _shell.CurrentSettings;

        SteamDirBox.Text = settings.SteamDir;
        MainDirBox.Text = settings.MainDir;
        DaddyDirBox.Text = settings.DaddyDir;
        BaseDirBox.Text = settings.BaseDir;

        ConfirmSwitchBox.IsChecked = settings.ConfirmSwitch;
        BackupBeforeSwitchBox.IsChecked = settings.BackupBeforeSwitch;
        SyncAutoLoginBox.IsChecked = settings.SyncAutoLogin;
        AutoLaunchBox.IsChecked = settings.AutoLaunchSteam;
        AutoRefreshBox.IsChecked = settings.AutoRefresh;

        RefreshSlider.Value = settings.SafeRefreshSeconds;
        RefreshValueText.Text = $"{settings.SafeRefreshSeconds} s";

        MainNameBox.Text = settings.MainDisplayName;
        DaddyNameBox.Text = settings.DaddyDisplayName;

        MainIdBox.Text = settings.MainExpectedSteamId;
        DaddyIdBox.Text = settings.DaddyExpectedSteamId;

        AutoUpdateDaddyBox.IsChecked = settings.AutoUpdateSteamDaddy;
        DaddyExeBox.Text = settings.SteamDaddyExePath;
        DaddyUpdateUrlBox.Text = settings.SteamDaddyUpdateUrl;
        SilentElevationBox.IsChecked = settings.SilentElevation;
        ShowUpdateState();

        // Interval control only matters while auto-refresh is on.
        IntervalPanel.Visibility = settings.AutoRefresh ? Visibility.Visible : Visibility.Collapsed;
        IntervalPanel.Opacity = settings.AutoRefresh ? 1 : 0;

        ClearDirty();
    }

    public void OnStatusUpdated(SystemStatus status)
    {
        _status = status;

        MainDetectedText.Text = status.Main?.Account is { } main && main.SteamId64.Length > 0
            ? $"Detected: {main.Display} · {main.SteamId64}"
            : "No account detected in this profile.";

        DaddyDetectedText.Text = status.Daddy?.Account is { } daddy && daddy.SteamId64.Length > 0
            ? $"Detected: {daddy.Display} · {daddy.SteamId64}"
            : "No account detected in this profile.";

        ValidatePaths();
    }

    public void OnBusyChanged(bool busy) { }

    // ---------------------------------------------------------------- validate

    private void ValidatePaths()
    {
        var problems = new List<string>();

        var steam = SteamDirBox.Text?.Trim() ?? "";
        var main = MainDirBox.Text?.Trim() ?? "";
        var daddy = DaddyDirBox.Text?.Trim() ?? "";

        if (steam.Length == 0 || main.Length == 0 || daddy.Length == 0)
            problems.Add("All three profile paths must be filled in.");

        var distinct = new[] { steam, main, daddy }
            .Where(p => p.Length > 0)
            .Select(p => p.TrimEnd('\\').ToLowerInvariant())
            .Distinct()
            .Count();

        if (distinct < 3 && steam.Length > 0 && main.Length > 0 && daddy.Length > 0)
            problems.Add("The three paths must all be different.");

        if (main.Length > 0 && !Directory.Exists(main) && _status.ActiveProfile != ProfileKind.Main)
            problems.Add($"Main profile folder not found: {main}");

        if (daddy.Length > 0 && !Directory.Exists(daddy) && _status.ActiveProfile != ProfileKind.Daddy)
            problems.Add($"Daddy profile folder not found: {daddy}");

        foreach (var id in new[] { MainIdBox.Text?.Trim(), DaddyIdBox.Text?.Trim() })
        {
            if (!string.IsNullOrWhiteSpace(id) && (id.Length != 17 || !id.All(char.IsDigit)))
            {
                problems.Add("A SteamID64 must be exactly 17 digits.");
                break;
            }
        }

        if (problems.Count == 0)
        {
            PathWarning.Visibility = Visibility.Collapsed;
        }
        else
        {
            PathWarning.Visibility = Visibility.Visible;
            PathWarningText.Text = string.Join("\n", problems);
        }
    }

    private void OnRefreshSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RefreshValueText is null) return;

        var seconds = (int)e.NewValue;
        RefreshValueText.Text = seconds >= 60
            ? $"{seconds / 60}m {seconds % 60}s".Replace(" 0s", "")
            : $"{seconds} s";

        MarkDirty();
    }

    // -------------------------------------------------------- live UI feedback

    /// <summary>Re-validates as the user types so mistakes surface immediately.</summary>
    private void OnPathChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        ValidatePaths();
        MarkDirty();
    }

    private void OnSteamIdChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        ValidatePaths();
        MarkDirty();
    }

    /// <summary>Collapses the interval control when auto-refresh is switched off.</summary>
    private void OnAutoRefreshToggled(object sender, RoutedEventArgs e)
    {
        if (IntervalPanel is null) return;

        var on = AutoRefreshBox.IsChecked == true;

        if (on) Anim.FadeIn(IntervalPanel);
        else Anim.FadeOut(IntervalPanel);

        if (_loaded) MarkDirty();
    }

    /// <summary>Opens a folder picker for whichever path button was clicked.</summary>
    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;

        var target = button.Tag as string ?? "";

        // SteamDaddy.exe is a file, not a folder, so it uses a file picker.
        if (target == "daddyexe")
        {
            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Locate SteamDaddy.exe",
                Filter = "SteamDaddy (SteamDaddy.exe)|SteamDaddy.exe|Programs (*.exe)|*.exe",
                CheckFileExists = true
            };

            var currentExe = DaddyExeBox.Text?.Trim() ?? "";
            if (currentExe.Length > 0)
            {
                var folder = Path.GetDirectoryName(currentExe);
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                    fileDialog.InitialDirectory = folder;
            }

            if (fileDialog.ShowDialog() == true)
            {
                DaddyExeBox.Text = fileDialog.FileName;
                MarkDirty();
            }

            return;
        }

        var box = target switch
        {
            "steam" => SteamDirBox,
            "main" => MainDirBox,
            "daddy" => DaddyDirBox,
            "base" => BaseDirBox,
            _ => null
        };

        if (box is null) return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose a folder",
            Multiselect = false
        };

        var current = box.Text?.Trim() ?? "";
        if (current.Length > 0 && Directory.Exists(current))
            dialog.InitialDirectory = current;

        if (dialog.ShowDialog() == true)
        {
            box.Text = dialog.FolderName;
            ValidatePaths();
            MarkDirty();
        }
    }

    // ------------------------------------------------- SteamDaddy auto-update

    /// <summary>Shows what we last installed, without touching the network.</summary>
    private void ShowUpdateState()
    {
        if (DaddyUpdateStatus is null) return;

        var state = SteamDaddyUpdater.LoadState();

        if (string.IsNullOrWhiteSpace(state.InstalledVersion))
        {
            DaddyUpdateStatus.Text = "No update installed by this app yet.";
            return;
        }

        var when = state.LastInstallUtc == DateTime.MinValue
            ? ""
            : $" on {state.LastInstallUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

        DaddyUpdateStatus.Text = $"Installed {state.InstalledVersion}{when}.";
    }

    /// <summary>Clears the custom link so the official source is used again.</summary>
    private void OnResetUpdateUrlClick(object sender, RoutedEventArgs e)
    {
        DaddyUpdateUrlBox.Text = Settings.DefaultSteamDaddyUpdateUrl;
        MarkDirty();
        _shell.SetStatus("Update link reset to the official source. Save to apply.");
    }

    /// <summary>
    /// Shows any instruction the SteamDaddy team marked as required, so it is
    /// visible here rather than only on their GitHub page.
    /// </summary>
    private async Task ShowUpstreamNoticeAsync(string apiUrl)
    {
        var notice = await UpstreamNotice.FetchAsync(apiUrl);

        if (notice is null || !notice.HasContent)
        {
            UpstreamNoticePanel.Visibility = Visibility.Collapsed;
            return;
        }

        UpstreamNoticeTitle.Text = notice.Version.Length > 0
            ? $"Required steps from the SteamDaddy team ({notice.Version})"
            : "Required steps from the SteamDaddy team";

        UpstreamNoticeText.Text = notice.Text;
        UpstreamNoticePanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Manual check. Forces past the throttle, because the user explicitly
    /// asked for it, but still installs only when the release actually differs.
    /// </summary>
    private async void OnCheckDaddyUpdateClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;

        var settings = _shell.CurrentSettings;

        // Use the path typed in the box even when it has not been saved yet.
        var typed = DaddyExeBox.Text?.Trim() ?? "";
        var original = settings.SteamDaddyExePath;
        settings.SteamDaddyExePath = typed;

        button.IsEnabled = false;
        DaddyUpdateStatus.Text = "Checking…";

        try
        {
            var progress = new Progress<string>(text => DaddyUpdateStatus.Text = text);
            var result = await SteamDaddyUpdater.RunAsync(settings, progress, force: true);

            DaddyUpdateStatus.Text = result.Message;
            _shell.SetStatus(result.Message);

            if (result.Outcome == UpdateOutcome.Updated)
            {
                MessageBox.Show(
                    result.Message + "\n\nOpen SteamDaddy and click Install Plugin to finish setup.",
                    "SteamDaddy updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Manual SteamDaddy update check failed", ex);
            DaddyUpdateStatus.Text = $"Check failed: {ex.Message}";
        }
        finally
        {
            settings.SteamDaddyExePath = original;
            button.IsEnabled = true;
        }
    }

    // ---------------------------------------------------------------- about

    /// <summary>
    /// Fills the About tab from the running executable, so the version shown
    /// is always the build actually in use rather than a hard-coded string
    /// that silently goes stale.
    /// </summary>
    private void LoadAboutInfo()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";

            // AppInfo owns the version, so the About tab and the title bar can
            // never disagree with each other.
            AboutVersionText.Text = AppInfo.CurrentVersion;

            AboutBuiltText.Text = exePath.Length > 0 && File.Exists(exePath)
                ? new FileInfo(exePath).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                : "unknown";

            AboutPathText.Text = exePath.Length > 0 ? exePath : "unknown";
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read build info: {ex.Message}");
            AboutVersionText.Text = "unavailable";
            AboutBuiltText.Text = "unavailable";
            AboutPathText.Text = "unavailable";
        }
    }

    /// <summary>Copies the build details, so they can be pasted into a bug report.</summary>
    private void OnCopyBuildInfoClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text =
                "Steam Profile Switcher — Aizenrex x Riyad\n" +
                $"Version : {AboutVersionText.Text}\n" +
                $"Built   : {AboutBuiltText.Text}\n" +
                $"Location: {AboutPathText.Text}";

            System.Windows.Clipboard.SetText(text);
            _shell.SetStatus("Build info copied.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not copy build info: {ex.Message}");
            _shell.SetStatus("Could not copy build info.");
        }
    }

    private void MarkDirty()
    {
        if (!_loaded || DirtyText is null) return;

        if (DirtyText.Visibility != Visibility.Visible)
            Anim.FadeIn(DirtyText);
    }

    private void ClearDirty()
    {
        if (DirtyText is null) return;
        DirtyText.Visibility = Visibility.Collapsed;
    }

    // ------------------------------------------------------------------ actions

    private void OnUseDetectedClick(object sender, RoutedEventArgs e)
    {
        var mainId = _status.Main?.Account?.SteamId64 ?? "";
        var daddyId = _status.Daddy?.Account?.SteamId64 ?? "";

        if (mainId.Length == 0 && daddyId.Length == 0)
        {
            MessageBox.Show("No accounts were detected in either profile.",
                "Nothing to copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (mainId.Length > 0) MainIdBox.Text = mainId;
        if (daddyId.Length > 0) DaddyIdBox.Text = daddyId;

        ValidatePaths();
        _shell.SetStatus("Detected account IDs copied into the lock fields.");
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ValidatePaths();

        if (PathWarning.Visibility == Visibility.Visible)
        {
            var proceed = MessageBox.Show(
                PathWarningText.Text + "\n\nSave anyway?",
                "Check the settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (proceed != MessageBoxResult.Yes) return;
        }

        var settings = _shell.CurrentSettings;

        settings.SteamDir = SteamDirBox.Text.Trim();
        settings.MainDir = MainDirBox.Text.Trim();
        settings.DaddyDir = DaddyDirBox.Text.Trim();
        settings.BaseDir = BaseDirBox.Text.Trim();

        settings.ConfirmSwitch = ConfirmSwitchBox.IsChecked == true;
        settings.BackupBeforeSwitch = BackupBeforeSwitchBox.IsChecked == true;
        settings.SyncAutoLogin = SyncAutoLoginBox.IsChecked == true;
        settings.AutoLaunchSteam = AutoLaunchBox.IsChecked == true;
        settings.AutoRefresh = AutoRefreshBox.IsChecked == true;
        settings.RefreshSeconds = (int)RefreshSlider.Value;

        settings.MainDisplayName = string.IsNullOrWhiteSpace(MainNameBox.Text) ? "Steam Main" : MainNameBox.Text.Trim();
        settings.DaddyDisplayName = string.IsNullOrWhiteSpace(DaddyNameBox.Text) ? "Steam Daddy" : DaddyNameBox.Text.Trim();

        settings.MainExpectedSteamId = MainIdBox.Text?.Trim() ?? "";
        settings.DaddyExpectedSteamId = DaddyIdBox.Text?.Trim() ?? "";

        settings.AutoUpdateSteamDaddy = AutoUpdateDaddyBox.IsChecked == true;
        settings.SteamDaddyExePath = DaddyExeBox.Text?.Trim() ?? "";

        // The link is saved as typed; empty means fall back to the official source.
        settings.SteamDaddyUpdateUrl = DaddyUpdateUrlBox.Text?.Trim() ?? "";

        // Register or remove the silent-elevation task to match the toggle.
        var wantSilent = SilentElevationBox.IsChecked == true;
        if (wantSilent != settings.SilentElevation || wantSilent != ElevatedLauncher.IsInstalled())
        {
            if (wantSilent)
            {
                var exe = System.IO.Path.Combine(settings.SteamDir, "steam.exe");
                var install = ElevatedLauncher.Install(exe);
                if (!install.Success)
                {
                    MessageBox.Show(
                        "Steam will still start, but Windows will keep asking for permission.\n\n" + install.Message,
                        "Could not set up silent elevation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    wantSilent = false;
                    SilentElevationBox.IsChecked = false;
                }
            }
            else
            {
                ElevatedLauncher.Uninstall();
            }
        }
        settings.SilentElevation = wantSilent;

        Store.SaveSettings(settings);
        Log.Info("Settings saved.");

        _shell.OnSettingsSaved();
        await _shell.RefreshAsync();

        LoadFromSettings();
        _shell.SetStatus("Settings saved.");

        MessageBox.Show("Settings saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Reset every setting to its default value?",
            "Reset settings", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        var defaults = new Settings();
        var settings = _shell.CurrentSettings;

        settings.SteamDir = defaults.SteamDir;
        settings.MainDir = defaults.MainDir;
        settings.DaddyDir = defaults.DaddyDir;
        settings.BaseDir = defaults.BaseDir;
        settings.ConfirmSwitch = defaults.ConfirmSwitch;
        settings.BackupBeforeSwitch = defaults.BackupBeforeSwitch;
        settings.SyncAutoLogin = defaults.SyncAutoLogin;
        settings.AutoLaunchSteam = defaults.AutoLaunchSteam;
        settings.AutoRefresh = defaults.AutoRefresh;
        settings.RefreshSeconds = defaults.RefreshSeconds;
        settings.MainDisplayName = defaults.MainDisplayName;
        settings.DaddyDisplayName = defaults.DaddyDisplayName;
        settings.MainExpectedSteamId = "";
        settings.DaddyExpectedSteamId = "";

        Store.SaveSettings(settings);
        Log.Info("Settings reset to defaults.");

        _shell.OnSettingsSaved();
        await _shell.RefreshAsync();

        LoadFromSettings();
        _shell.SetStatus("Settings reset to defaults.");
    }
}
