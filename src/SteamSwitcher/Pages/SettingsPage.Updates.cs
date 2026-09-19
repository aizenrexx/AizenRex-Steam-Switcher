using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

/// <summary>
/// The Updates and Credits half of the About tab.
///
/// Kept in its own file so the settings form logic stays readable. The XAML
/// declares the controls; the handlers live here.
/// </summary>
public partial class SettingsPage
{
    private UpdateCheck? _lastCheck;
    private bool _checking;

    /// <summary>Fills the credit and version lines that never change at runtime.</summary>
    private void LoadCreditInfo()
    {
        CreditAuthorText.Text = AppInfo.Author;
        CreditRoleText.Text = AppInfo.AuthorRole;
        CreditProductText.Text = AppInfo.ProductName;
        CreditVersionText.Text = $"v{AppInfo.CurrentVersion}  -  {AppInfo.LicenseName} licence";
        UpdateCurrentVersionText.Text = $"Installed: v{AppInfo.CurrentVersion}";
        UpdateStatusText.Text = "Not checked yet.";
        UpdateNotesText.Visibility = Visibility.Collapsed;
        InstallUpdateButton.IsEnabled = false;
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (_checking) return;

        _checking = true;
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking GitHub...";
        UpdateNotesText.Visibility = Visibility.Collapsed;
        InstallUpdateButton.IsEnabled = false;

        try
        {
            var check = await UpdateService.CheckAsync();
            _lastCheck = check;

            UpdateStatusText.Text = check.Message;

            if (check.Notes.Length > 0)
            {
                UpdateNotesText.Text = check.Notes;
                UpdateNotesText.Visibility = Visibility.Visible;
            }

            InstallUpdateButton.IsEnabled = check.HasUpdate;
            InstallUpdateButton.Content = check.IsPortableOnly
                ? "Open download page"
                : "Download and install";
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            UpdateStatusText.Text = "Could not check for updates.";
        }
        finally
        {
            _checking = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async void OnInstallUpdateClick(object sender, RoutedEventArgs e)
    {
        var check = _lastCheck;
        if (check is null || !check.HasUpdate) return;

        // No installer was published for that release: send the user to the
        // releases page instead of guessing at a portable upgrade.
        if (check.IsPortableOnly)
        {
            UpdateService.OpenReleasesPage();
            UpdateStatusText.Text = "Opened the releases page. Download the portable ZIP there.";
            return;
        }

        InstallUpdateButton.IsEnabled = false;
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = $"Downloading {check.AssetName}...";

        var progress = new Progress<string>(text => UpdateStatusText.Text = text);
        var download = await UpdateService.DownloadAsync(check, progress);

        if (!download.Success)
        {
            UpdateStatusText.Text = download.Message;
            InstallUpdateButton.IsEnabled = true;
            CheckUpdateButton.IsEnabled = true;
            return;
        }

        var launch = UpdateService.LaunchInstaller(download.Message);
        UpdateStatusText.Text = launch.Message;

        if (launch.Success)
        {
            // The installer replaces this exe, so get out of its way.
            await Task.Delay(700);
            Application.Current.Shutdown();
        }
        else
        {
            InstallUpdateButton.IsEnabled = true;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void OnOpenRepoClick(object sender, RoutedEventArgs e) =>
        UpdateService.OpenUrl(AppInfo.RepositoryUrl);

    private void OnOpenReleasesClick(object sender, RoutedEventArgs e) =>
        UpdateService.OpenUrl(AppInfo.ReleasesUrl);

    private void OnOpenIssuesClick(object sender, RoutedEventArgs e) =>
        UpdateService.OpenUrl(AppInfo.IssuesUrl);

    private void OnCopyCreditClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text =
                $"{AppInfo.ProductName}\n" +
                $"{AppInfo.Author}\n" +
                $"Version : v{AppInfo.CurrentVersion}\n" +
                $"Licence : {AppInfo.LicenseName}\n" +
                $"{AppInfo.RepositoryUrl}";

            Clipboard.SetText(text);
            UpdateStatusText.Text = "Credit copied to the clipboard.";
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not copy credit: {ex.Message}");
        }
    }
}