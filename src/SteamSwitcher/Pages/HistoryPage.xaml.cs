using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class HistoryPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;

    // Set once the constructor has finished. Filter controls raise their
    // change events during XAML load, before the fields below exist.
    private bool _ready;
    private List<HistoryEntry> _all = new();

    private sealed record Stat(string Label, string Value);

    public HistoryPage()
    {
        InitializeComponent();

        // Named controls exist from here on, so the filter guard can lift
        // before the first real load.
        _ready = true;

        Reload();
    }

    public void OnStatusUpdated(SystemStatus status) => Reload();

    public void OnBusyChanged(bool busy) { }

    private void Reload()
    {
        _all = Store.LoadHistory();

        var switches = _all.Where(h => h.Action.StartsWith("Switch", StringComparison.OrdinalIgnoreCase)).ToList();
        var lastSuccess = _all.FirstOrDefault(h => h.Success);

        StatsHost.ItemsSource = new List<Stat>
        {
            new("TOTAL EVENTS", _all.Count.ToString()),
            new("SWITCHES", switches.Count.ToString()),
            new("FAILED", _all.Count(h => !h.Success).ToString()),
            new("LAST SUCCESS", lastSuccess is null ? "never" : Format.Relative(lastSuccess.Time))
        };

        ApplyFilter();
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!_ready) return;
        IEnumerable<HistoryEntry> rows = _all;

        var index = ResultFilter.SelectedIndex;
        if (index == 1) rows = rows.Where(h => h.Success);
        else if (index == 2) rows = rows.Where(h => !h.Success);

        var query = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(h =>
                h.Action.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                h.Message.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        HistoryGrid.ItemsSource = rows.ToList();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_all.Count == 0)
        {
            _shell.SetStatus("History is already empty.");
            return;
        }

        var answer = MessageBox.Show(
            $"Delete all {_all.Count} history entries?\n\nThis cannot be undone.",
            "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        Store.ClearHistory();
        Reload();
        _shell.SetStatus("History cleared.");
    }
}
