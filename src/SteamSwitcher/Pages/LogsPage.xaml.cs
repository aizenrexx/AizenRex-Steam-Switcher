using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class LogsPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;

    // Set once the constructor has finished. Filter controls raise their
    // change events during XAML load, before the fields below exist.
    private bool _ready;
    private List<string> _lines = new();

    public LogsPage()
    {
        InitializeComponent();

        // Named controls exist from here on, so the filter guard can lift
        // before the first real load.
        _ready = true;

        Reload();
    }

    public void OnStatusUpdated(SystemStatus status) { }

    public void OnBusyChanged(bool busy) { }

    private void Reload()
    {
        _lines = Log.ReadFile(limit: 2000);
        SubtitleText.Text = $"Application log · {Format.HumanSize(Log.CurrentSizeBytes())} · rotates automatically at 1 MB.";
        ApplyFilter();
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!_ready) return;
        IEnumerable<string> rows = _lines;

        var level = LevelFilter.SelectedIndex switch
        {
            1 => "| ERROR",
            2 => "| WARN",
            3 => "| INFO",
            4 => "| DEBUG",
            _ => null
        };

        if (level is not null)
            rows = rows.Where(l => l.Contains(level, StringComparison.OrdinalIgnoreCase));

        var query = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
            rows = rows.Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase));

        var list = rows.Reverse().ToList();   // newest first
        LogList.ItemsSource = list;

        CountText.Text = _lines.Count == 0
            ? "The log is empty."
            : $"{list.Count} of {_lines.Count} lines · newest first";
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        Reload();
        _shell.SetStatus("Log reloaded.");
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var result = Tools.ExportLog();
        _shell.SetStatus(result.Message);

        MessageBox.Show(result.Message, result.Success ? "Exported" : "Export failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Delete the log file and its archives?",
            "Clear logs", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        Log.Clear();
        Reload();
        _shell.SetStatus("Logs cleared.");
    }
}
