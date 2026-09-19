using System.IO;
using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class BackupsPage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;
    private SystemStatus _status = new();

    public BackupsPage()
    {
        InitializeComponent();
        Reload();
    }

    public void OnStatusUpdated(SystemStatus status)
    {
        _status = status;

        var settings = _shell.CurrentSettings;
        ((ComboBoxItem)RestoreTarget.Items[0]).Content = $"Restore to {settings.MainDisplayName}";
        ((ComboBoxItem)RestoreTarget.Items[1]).Content = $"Restore to {settings.DaddyDisplayName}";

        BackupActiveButton.IsEnabled = status.SteamFolderExists && !_shell.IsBusy;
        Reload();
    }

    public void OnBusyChanged(bool busy)
    {
        BackupActiveButton.IsEnabled = !busy && _status.SteamFolderExists;
    }

    private void Reload()
    {
        var backups = BackupService.List();
        BackupGrid.ItemsSource = backups;

        var totalSize = backups.Sum(b => b.SizeBytes);
        CountText.Text = backups.Count == 0
            ? "No backups yet."
            : $"{backups.Count} backup(s) · {Format.HumanSize(totalSize)} total";
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = BackupGrid.SelectedItem as BackupEntry;
        var has = selected is not null;

        RestoreButton.IsEnabled = has && !_shell.IsBusy;
        DeleteButton.IsEnabled = has && !_shell.IsBusy;
        RestoreTarget.IsEnabled = has;

        SelectedText.Text = selected is null
            ? "Select a backup to restore or delete it"
            : $"{selected.Name} · {selected.Items.Count} item(s) from {selected.Profile}";
    }

    // ------------------------------------------------------------------ actions

    private async void OnBackupActiveClick(object sender, RoutedEventArgs e)
    {
        var settings = _shell.CurrentSettings;
        var label = Format.ProfileLabel(_status.ActiveProfile);

        _shell.SetBusy(true, "Creating backup…");
        var result = await Task.Run(() => BackupService.Create(settings.SteamDir, label));
        _shell.SetBusy(false, result.Message);

        Reload();

        MessageBox.Show(result.Message, result.Success ? "Backup created" : "Backup failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var result = Tools.OpenFolder(AppPaths.BackupDir);
        _shell.SetStatus(result.Message);
    }

    private async void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is not BackupEntry backup) return;

        var settings = _shell.CurrentSettings;
        var toMain = RestoreTarget.SelectedIndex == 0;
        var targetKind = toMain ? ProfileKind.Main : ProfileKind.Daddy;
        var label = toMain ? settings.MainDisplayName : settings.DaddyDisplayName;

        // Resolve where that profile actually lives right now: it may be the
        // active Steam folder rather than the parked folder.
        var targetDir = _status.ActiveProfile == targetKind
            ? settings.SteamDir
            : toMain ? settings.MainDir : settings.DaddyDir;

        if (!Directory.Exists(targetDir))
        {
            MessageBox.Show(
                $"The {label} profile folder was not found at:\n{targetDir}",
                "Restore failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SteamProcesses.IsSteamRunning())
        {
            var closeFirst = MessageBox.Show(
                "Steam is running. Restoring login data while Steam is open can be overwritten when it exits.\n\nClose Steam first?",
                "Steam is running", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            if (closeFirst == MessageBoxResult.Cancel) return;

            if (closeFirst == MessageBoxResult.Yes)
            {
                _shell.SetBusy(true, "Closing Steam…");
                var close = await SteamProcesses.CloseSteamAsync();
                _shell.SetBusy(false, close.Message);
                if (!close.Success)
                {
                    MessageBox.Show(close.Message, "Could not close Steam",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
        }

        var answer = MessageBox.Show(
            $"Restore {backup.Items.Count} item(s) from '{backup.Name}' into {label}?\n\n" +
            $"Target: {targetDir}\n\nExisting files with the same names will be overwritten.",
            "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        _shell.SetBusy(true, "Restoring backup…");
        var result = await Task.Run(() => BackupService.Restore(backup, targetDir));
        _shell.SetBusy(false, result.Message);

        await _shell.RefreshAsync();

        MessageBox.Show(result.Message, result.Success ? "Restored" : "Restore failed",
            MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is not BackupEntry backup) return;

        var answer = MessageBox.Show(
            $"Delete backup '{backup.Name}'?\n\nThis cannot be undone.",
            "Delete backup", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        var result = BackupService.Delete(backup);
        _shell.SetStatus(result.Message);
        Reload();

        if (!result.Success)
            MessageBox.Show(result.Message, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
