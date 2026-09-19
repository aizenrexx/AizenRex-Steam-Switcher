using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class ToolsPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;
    private SystemStatus _status = new();

    public ToolsPage()
    {
        InitializeComponent();
        LoadRecent();
    }

    public void OnStatusUpdated(SystemStatus status)
    {
        _status = status;

        ClearCacheButton.IsEnabled = status.SteamFolderExists && !status.SteamRunning && !_shell.IsBusy;
        RepairServiceButton.IsEnabled = status.SteamFolderExists && !_shell.IsBusy;
        SyncLoginButton.IsEnabled = status.SteamFolderExists && !_shell.IsBusy;

        LoadRecent();
    }

    public void OnBusyChanged(bool busy)
    {
        ClearCacheButton.IsEnabled = !busy && _status.SteamFolderExists && !_status.SteamRunning;
        RepairServiceButton.IsEnabled = !busy && _status.SteamFolderExists;
        SyncLoginButton.IsEnabled = !busy && _status.SteamFolderExists;
    }

    private void LoadRecent()
    {
        var recent = Store.LoadUserData().RecentGames;

        if (recent.Count == 0)
        {
            RecentGrid.Visibility = Visibility.Collapsed;
            NoRecentText.Visibility = Visibility.Visible;
            return;
        }

        RecentGrid.Visibility = Visibility.Visible;
        NoRecentText.Visibility = Visibility.Collapsed;
        RecentGrid.ItemsSource = recent;
    }

    // --------------------------------------------------------- Steam maintenance

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Delete Steam's web cache?\n\nSteam will rebuild it on next start. Nothing else is removed.",
            "Clear web cache", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        var result = Tools.ClearWebCache(_shell.CurrentSettings.SteamDir);
        _shell.SetStatus(result.Message);

        MessageBox.Show(result.Message, result.Success ? "Done" : "Could not clear cache",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private async void OnRepairServiceClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Run the Steam service repair?\n\nThis can take up to two minutes.",
            "Repair Steam service", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        _shell.SetBusy(true, "Repairing the Steam service…");
        var result = await Tools.RepairSteamServiceAsync(_shell.CurrentSettings.SteamDir);
        _shell.SetBusy(false, result.Message);

        MessageBox.Show(result.Message, result.Success ? "Repair finished" : "Repair failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void OnSyncLoginClick(object sender, RoutedEventArgs e)
    {
        var result = AutoLoginSync.SyncFor(_shell.CurrentSettings.SteamDir);
        _shell.SetStatus(result.Message);

        MessageBox.Show(result.Message, result.Success ? "Account synced" : "Sync failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    // ------------------------------------------------------------------- folders

    private void Open(string path)
    {
        var result = Tools.OpenFolder(path);
        _shell.SetStatus(result.Message);

        if (!result.Success)
            MessageBox.Show(result.Message, "Folder not found", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnOpenSteamClick(object sender, RoutedEventArgs e) => Open(_shell.CurrentSettings.SteamDir);
    private void OnOpenMainClick(object sender, RoutedEventArgs e) => Open(_shell.CurrentSettings.MainDir);
    private void OnOpenDaddyClick(object sender, RoutedEventArgs e) => Open(_shell.CurrentSettings.DaddyDir);
    private void OnOpenAppClick(object sender, RoutedEventArgs e) => Open(AppPaths.AppDir);
    private void OnOpenBackupsClick(object sender, RoutedEventArgs e) => Open(AppPaths.BackupDir);
    private void OnOpenLogsClick(object sender, RoutedEventArgs e) => Open(AppPaths.LogDir);
    private void OnOpenExportsClick(object sender, RoutedEventArgs e) => Open(AppPaths.ExportDir);
}
