using System.Windows;
using System.Windows.Threading;
using SteamSwitcher.Core;

namespace SteamSwitcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureAll();
        Log.Info($"=== {AppInfo.DisplayVersion} starting ===");
        Log.Info($"Application folder: {AppPaths.AppDir}");
        Log.Info($"Elevated: {SteamProcesses.IsElevated()}");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Error("Unhandled domain exception", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception", e.Exception);

        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}\n\nThe details were written to logs\\steam-switcher.log.",
            AppInfo.ProductName,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Keep the app alive; a single page fault should not close the window.
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info($"=== {AppInfo.ShortName} closing ===");
        base.OnExit(e);
    }
}
