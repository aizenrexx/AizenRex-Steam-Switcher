using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class LibraryPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;

    // Set once the constructor has finished. Filter controls raise their
    // change events during XAML load, before the fields below exist.
    private bool _ready;
    private SystemStatus _status = new();
    private List<Row> _allRows = new();

    public sealed class Row
    {
        public string Profile { get; set; } = "";
        public string Name { get; set; } = "";
        public string AppId { get; set; } = "";
        public string InstallDir { get; set; } = "";
        public long SizeBytes { get; set; }
        public bool IsRedistributable { get; set; }
        public string SizeText => Format.HumanSize(SizeBytes);
    }

    public LibraryPage()
    {
        InitializeComponent();
        _ready = true;
    }

    public void OnStatusUpdated(SystemStatus status)
    {
        _status = status;
        _allRows = new List<Row>();

        void Collect(string profile, FolderReport? report)
        {
            if (report is null) return;
            foreach (var game in report.Games)
            {
                _allRows.Add(new Row
                {
                    Profile = profile,
                    Name = game.Name,
                    AppId = game.AppId,
                    InstallDir = game.InstallDir,
                    SizeBytes = game.SizeBytes,
                    IsRedistributable = game.IsRedistributable
                });
            }
        }

        Collect("Main", status.Main);
        Collect("Daddy", status.Daddy);

        SubtitleText.Text = _allRows.Count == 0
            ? "No installed games were found. Check the profile paths in Settings."
            : "Everything installed across both profiles. Select a row to launch it.";

        ApplyFilter();
    }

    public void OnBusyChanged(bool busy)
    {
        LaunchButton.IsEnabled = !busy && GamesGrid.SelectedItem is Row;
    }

    // ------------------------------------------------------------------- filter

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!_ready) return;
        if (_allRows.Count == 0)
        {
            GamesGrid.ItemsSource = null;
            CountText.Text = "Nothing to show.";
            return;
        }

        IEnumerable<Row> rows = _allRows;

        if (HideRedist.IsChecked == true)
            rows = rows.Where(r => !r.IsRedistributable);

        var profileIndex = ProfileFilter.SelectedIndex;
        if (profileIndex == 1) rows = rows.Where(r => r.Profile == "Main");
        else if (profileIndex == 2) rows = rows.Where(r => r.Profile == "Daddy");

        var query = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(r =>
                r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.AppId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var list = rows
            .OrderBy(r => r.Profile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        GamesGrid.ItemsSource = list;

        var totalSize = list.Sum(r => r.SizeBytes);
        CountText.Text = $"{list.Count} of {_allRows.Count} entries · {Format.HumanSize(totalSize)}";
    }

    // ------------------------------------------------------------------ actions

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not Row row)
        {
            SelectedNameText.Text = "Select a game to launch it";
            SelectedHintText.Text = "";
            LaunchButton.IsEnabled = false;
            LaunchArgsBox.IsEnabled = false;
            return;
        }

        var activeProfile = Format.ProfileLabel(_status.ActiveProfile);
        var isActiveProfile = string.Equals(row.Profile, activeProfile, StringComparison.OrdinalIgnoreCase);

        SelectedNameText.Text = row.Name;

        SelectedHintText.Text = isActiveProfile
            ? $"AppID {row.AppId} · {row.SizeText}"
            : $"This game belongs to the {row.Profile} profile — switch to it first.";

        LaunchButton.IsEnabled = isActiveProfile && !_shell.IsBusy;
        LaunchArgsBox.IsEnabled = isActiveProfile;

        // Remember any saved launch options.
        var userData = Store.LoadUserData();
        LaunchArgsBox.Text = userData.LaunchArgs.TryGetValue(row.AppId, out var saved) ? saved : "";
    }

    private void OnLaunchClick(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not Row row) return;

        var args = LaunchArgsBox.Text?.Trim() ?? "";

        // Persist launch options per app.
        var userData = Store.LoadUserData();
        if (args.Length > 0) userData.LaunchArgs[row.AppId] = args;
        else userData.LaunchArgs.Remove(row.AppId);

        userData.RecentGames.RemoveAll(g => g.AppId == row.AppId);
        userData.RecentGames.Insert(0, new Store.RecentGame
        {
            AppId = row.AppId,
            Name = row.Name,
            Profile = row.Profile,
            Time = DateTime.Now
        });
        if (userData.RecentGames.Count > 20)
            userData.RecentGames = userData.RecentGames.Take(20).ToList();

        Store.SaveUserData(userData);

        var result = SteamProcesses.LaunchGame(_shell.CurrentSettings.SteamDir, row.AppId, args);
        _shell.SetStatus(result.Message);

        if (!result.Success)
            MessageBox.Show(result.Message, "Launch failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var result = Tools.ExportLibraryCsv(_status);
        _shell.SetStatus(result.Message);

        MessageBox.Show(result.Message, result.Success ? "Exported" : "Export failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
