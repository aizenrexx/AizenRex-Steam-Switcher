using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class DiagnosticsPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;
    private SystemStatus _status = new();

    private sealed record CheckView(string Name, string Detail, string Glyph, Brush Color);

    public DiagnosticsPage()
    {
        InitializeComponent();
    }

    public void OnStatusUpdated(SystemStatus status)
    {
        _status = status;
        var settings = _shell.CurrentSettings;
        var report = Diagnostics.Run(settings, status);

        // Score
        ScoreText.Text = report.Score.ToString();
        ScoreBar.Value = report.Score;
        GradeText.Text = report.Grade;

        var scoreBrush = report.Score >= 90 ? (Brush)FindResource("SuccessBrush")
            : report.Score >= 75 ? (Brush)FindResource("AccentBrush")
            : report.Score >= 50 ? (Brush)FindResource("WarningBrush")
            : (Brush)FindResource("DangerBrush");

        GradeText.Foreground = scoreBrush;
        ScoreBar.Foreground = scoreBrush;

        var failed = report.Checks.Count(c => c.Level == CheckLevel.Fail);
        var warned = report.Checks.Count(c => c.Level == CheckLevel.Warn);

        SummaryText.Text = failed == 0 && warned == 0
            ? "All checks passed. Switching is safe."
            : $"{failed} failing, {warned} warning, {report.Checks.Count - failed - warned} passing.";

        // Folder state
        ActivePathText.Text = settings.SteamDir;
        MainPathText.Text = settings.MainDir;
        DaddyPathText.Text = settings.DaddyDir;

        SetState(ActiveStateText, status.SteamFolderExists,
            status.SteamFolderExists ? $"present · {Format.ProfileLabel(status.ActiveProfile)}" : "missing");

        SetState(MainStateText, status.MainFolderExists || status.ActiveProfile == ProfileKind.Main,
            status.ActiveProfile == ProfileKind.Main ? "active" : status.MainFolderExists ? "parked" : "missing");

        SetState(DaddyStateText, status.DaddyFolderExists || status.ActiveProfile == ProfileKind.Daddy,
            status.ActiveProfile == ProfileKind.Daddy ? "active" : status.DaddyFolderExists ? "parked" : "missing");

        // Checks list
        ChecksHost.ItemsSource = report.Checks.Select(c => new CheckView(
            c.Name,
            c.Detail,
            c.Level switch
            {
                CheckLevel.Pass => "\uE73E",   // checkmark
                CheckLevel.Warn => "\uE7BA",   // warning
                _ => "\uE711"                  // cross
            },
            c.Level switch
            {
                CheckLevel.Pass => (Brush)FindResource("SuccessBrush"),
                CheckLevel.Warn => (Brush)FindResource("WarningBrush"),
                _ => (Brush)FindResource("DangerBrush")
            })).ToList();

        // Repair availability
        RepairButton.IsEnabled = _shell.Engine.BuildRecoveryPlan(status).Count == 1
                                 && status.IsElevated
                                 && !_shell.IsBusy;
    }

    private void SetState(TextBlock target, bool ok, string text)
    {
        target.Text = text;
        target.Foreground = ok
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("DangerBrush");
    }

    public void OnBusyChanged(bool busy)
    {
        TestButton.IsEnabled = !busy;
        if (!busy) OnStatusUpdated(_status);
    }

    // ------------------------------------------------------------------ actions

    private async void OnRerunClick(object sender, RoutedEventArgs e) => await _shell.RefreshAsync();

    private async void OnRepairClick(object sender, RoutedEventArgs e)
    {
        var plan = _shell.Engine.BuildRecoveryPlan(_status);
        if (plan.Count != 1)
        {
            MessageBox.Show("There is no single safe repair for this layout.",
                "Repair not available", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(
            $"Restore '{System.IO.Path.GetFileName(plan[0].From)}' as the active Steam folder?",
            "Repair folder layout", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        _shell.SetBusy(true, "Repairing…");
        var result = await Task.Run(() => _shell.Engine.Recover());
        _shell.SetBusy(false, result.Message);
        await _shell.RefreshAsync();

        MessageBox.Show(result.Message, result.Success ? "Repaired" : "Repair failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private async void OnTestConnectivityClick(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        ConnectivityText.Text = "Testing…";

        try
        {
            var report = await Diagnostics.CheckConnectivityAsync();

            string Mark(bool ok) => ok ? "OK" : "unreachable";

            var lines = new List<string>
            {
                $"DNS lookup: {Mark(report.Dns)}",
                $"Internet: {Mark(report.Internet)}",
                $"Steam Store: {Mark(report.SteamStore)}",
                $"Steam Community: {Mark(report.SteamCommunity)}"
            };

            if (report.Details.Count > 0)
                lines.Add("");
            lines.AddRange(report.Details.Take(4));

            ConnectivityText.Text = string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            ConnectivityText.Text = $"Test failed: {ex.Message}";
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }
}
