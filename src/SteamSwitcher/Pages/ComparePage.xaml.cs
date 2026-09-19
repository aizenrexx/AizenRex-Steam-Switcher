using System.Windows;
using System.Windows.Controls;
using SteamSwitcher.Core;

namespace SteamSwitcher.Pages;

public partial class ComparePage : Page, IRefreshablePage
{
    private MainWindow _shell => MainWindow.Current!;

    public ComparePage()
    {
        InitializeComponent();
    }

    public void OnStatusUpdated(SystemStatus status)
    {
        var settings = _shell.CurrentSettings;
        var report = Diagnostics.Compare(status);

        MainHeader.Text = settings.MainDisplayName.ToUpperInvariant();
        DaddyHeader.Text = settings.DaddyDisplayName.ToUpperInvariant();

        MainGames.Text = report.MainGames.ToString();
        DaddyGames.Text = report.DaddyGames.ToString();

        MainSize.Text = report.MainSizeText;
        DaddySize.Text = report.DaddySizeText;

        MainMods.Text = report.MainMods.ToString();
        DaddyMods.Text = report.DaddyMods.ToString();

        MainManifests.Text = report.MainManifests.ToString();
        DaddyManifests.Text = report.DaddyManifests.ToString();

        Fill(MainOnlyList, MainOnlyEmpty, MainOnlyHeader,
            $"Only in {settings.MainDisplayName}", report.MainOnly);

        Fill(DaddyOnlyList, DaddyOnlyEmpty, DaddyOnlyHeader,
            $"Only in {settings.DaddyDisplayName}", report.DaddyOnly);

        Fill(SharedList, SharedEmpty, SharedHeader,
            "In both profiles", report.SharedGames);
    }

    private static void Fill(ListBox list, TextBlock empty, TextBlock header, string title, List<string> items)
    {
        header.Text = items.Count > 0 ? $"{title} ({items.Count})" : title;

        if (items.Count == 0)
        {
            list.Visibility = Visibility.Collapsed;
            empty.Visibility = Visibility.Visible;
            list.ItemsSource = null;
            return;
        }

        list.Visibility = Visibility.Visible;
        empty.Visibility = Visibility.Collapsed;
        list.ItemsSource = items;
    }

    public void OnBusyChanged(bool busy) { }
}
