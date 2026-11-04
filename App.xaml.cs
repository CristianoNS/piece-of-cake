using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PieceOfCake.Services;

namespace PieceOfCake;

public partial class App : Application
{
    private Mutex? _mutex;
    private TrayService? _tray;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Log(args.Exception);
            args.Handled = true;
        };

        _mutex = new Mutex(true, @"Local\PieceOfCake.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        var background = e.Args.Any(a => string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));

        try
        {
            if (background)
            {
                // Give Explorer a moment to create the notification area after logon.
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            var settings = new SettingsStore();
            settings.Load();
            StartupManager.MigrateLegacyEntry();

            _window = new MainWindow(settings);
            _tray = new TrayService();
            _tray.OpenRequested += () => _window.ToggleFlyout();
            _tray.SettingsFolderRequested += OpenSettingsFolder;
            _tray.ExitRequested += Shutdown;

            if (!background)
            {
                _window.ToggleFlyout();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            MessageBox.Show(
                "Nie udało się uruchomić Piece of Cake:\n" + ex.Message,
                "Piece of Cake",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static void OpenSettingsFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SettingsStore.DirectoryPath}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Logger.Log(ex);
        }
    }
}
