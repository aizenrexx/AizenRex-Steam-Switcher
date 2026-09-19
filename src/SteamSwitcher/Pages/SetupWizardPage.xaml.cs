using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

/// <summary>
/// First-run wizard. On a machine with nothing installed it installs Steam,
/// creates the profile folders, and walks the user through one sign-in per
/// profile. Every step reports what it actually did.
/// </summary>
public partial class SetupWizardPage : Page
{
    private readonly ObservableCollection<string> _log = new();
    private CancellationTokenSource? _cts;
    private bool _running;

    public event EventHandler? SetupFinished;

    public SetupWizardPage()
    {
        InitializeComponent();
        StepLog.ItemsSource = _log;
    }

    private void Say(string line)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressText.Text = line;
            _log.Add("• " + line);
        });
        Log.Info($"[setup] {line}");
    }

    private void SetProgress(double value) =>
        Dispatcher.Invoke(() => SetupProgress.Value = value);

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _running = true;
        _cts = new CancellationTokenSource();

        StartButton.IsEnabled = false;
        SkipButton.Content = "Cancel";
        StepLayoutCard.IsEnabled = false;
        StepProgressCard.Visibility = Visibility.Visible;

        var layout = SingleOption.IsChecked == true ? ProfileLayout.Single : ProfileLayout.Dual;
        var wantDaddy = InstallDaddyBox.IsChecked == true;

        try
        {
            await RunSetupAsync(layout, wantDaddy, _cts.Token);
        }
        catch (Exception ex)
        {
            Log.Error("Setup failed", ex);
            Say($"Setup stopped: {ex.Message}");
        }
        finally
        {
            _running = false;
            SkipButton.Content = "Close";
            StartButton.IsEnabled = true;
        }
    }

    private async Task RunSetupAsync(ProfileLayout layout, bool wantDaddy, CancellationToken ct)
    {
        var settings = Store.LoadSettings();
        var progress = new Progress<string>(Say);

        // ---- 1. Steam itself -------------------------------------------------
        SetProgress(5);
        var found = SteamInstaller.Detect();

        if (found.Installed)
        {
            Say($"Found Steam at {found.Path}.");
        }
        else
        {
            Say("Steam is not installed — downloading it now.");
            var install = await FirstRunService.EnsureSteamAsync(settings, progress, ct);
            Say(install.Message);
            if (!install.Success)
            {
                Finish(false, "Steam could not be installed, so setup stopped. Nothing was changed.");
                return;
            }
        }
        SetProgress(30);

        // ---- 2. Profile folders ---------------------------------------------
        var folders = FirstRunService.CreateProfileFolders(settings, layout);
        Say(folders.Message);
        if (!folders.Success)
        {
            Finish(false, folders.Message);
            return;
        }
        SetProgress(45);

        // ---- 3. Sign in, once per profile -----------------------------------
        Dispatcher.Invoke(() => LoginHintCard.Visibility = Visibility.Visible);

        var mainDone = await SignInAsync(settings, settings.SteamDir, isMain: true, progress, ct);
        SetProgress(70);

        if (layout == ProfileLayout.Dual && mainDone)
        {
            Dispatcher.Invoke(() => WaitingText.Text = "Now sign in to your second account…");
            await SignInAsync(settings, settings.DaddyDir, isMain: false, progress, ct);
        }
        SetProgress(85);

        Dispatcher.Invoke(() => LoginHintCard.Visibility = Visibility.Collapsed);

        // ---- 4. SteamDaddy (optional, never fatal) ---------------------------
        if (wantDaddy && layout == ProfileLayout.Dual)
        {
            Say("Installing SteamDaddy…");
            try
            {
                var upd = await SteamDaddyUpdater.RunAsync(settings, progress, force: true, ct);
                Say(upd.Message);
            }
            catch (Exception ex)
            {
                // A SteamDaddy problem must never block finishing setup.
                Say($"SteamDaddy step skipped: {ex.Message}");
            }
        }

        SetProgress(100);
        FirstRunService.MarkComplete();
        Finish(true, "Steam is ready and your profiles are set up. Switching from now on is automatic — you will not be asked to sign in again.");
    }

    private async Task<bool> SignInAsync(
        Settings settings, string dir, bool isMain, IProgress<string> progress, CancellationToken ct)
    {
        var label = isMain ? "main" : "second";

        // Already signed in for this folder?
        var existing = LoginUsers.Read(dir);
        if (existing.Count > 0)
        {
            var acct = existing[0];
            Say($"The {label} profile is already signed in as {acct.PersonaName}.");
            FirstRunService.RememberAccount(settings, isMain, acct);
            return true;
        }

        var (result, account) = await FirstRunService.LoginToProfileAsync(
            dir, progress, TimeSpan.FromMinutes(5), ct);

        Say(result.Message);

        if (account is not null)
        {
            FirstRunService.RememberAccount(settings, isMain, account);
            return true;
        }

        return false;
    }

    private void Finish(bool ok, string message)
    {
        Dispatcher.Invoke(() =>
        {
            DoneCard.Visibility = Visibility.Visible;
            DoneText.Text = message;
            SubtitleText.Text = ok ? "Setup complete." : "Setup did not finish.";
            StartButton.Content = ok ? "Done" : "Try again";
            StartButton.IsEnabled = true;
        });
    }

    private async void OnSkipClick(object sender, RoutedEventArgs e)
    {
        if (_running)
        {
            _cts?.Cancel();
            Say("Cancelled. Nothing further was changed.");
            return;
        }

        SetupFinished?.Invoke(this, EventArgs.Empty);
        await LeaveWizardAsync();
    }

    /// <summary>
    /// Hands control back to the shell. If setup never completed we do NOT
    /// write the marker, so the wizard returns on the next start instead of
    /// silently leaving the user on a broken dashboard.
    /// </summary>
    private async Task LeaveWizardAsync()
    {
        try
        {
            if (MainWindow.Current is { } shell)
                await shell.ResumeAfterSetupAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Could not return to the dashboard", ex);
        }
    }
}
