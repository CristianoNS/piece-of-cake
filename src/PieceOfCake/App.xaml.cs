using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PieceOfCake.Services;

namespace PieceOfCake;

[SuppressMessage("Design", "CA1001", Justification = "Resources are released in OnExit.")]
public partial class App : Application
{
    private Mutex? _mutex;
    private TrayService? _tray;
    private MainWindow? _window;
    private SettingsStore? _settings;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The flyout is small and mostly static: software rendering avoids loading the GPU driver (about 18 MB less).
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Log(args.Exception);
            args.Handled = true;
        };

        // Errors outside the UI thread: log them instead of losing them. The first one ends the process.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Logger.Log(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Log(args.Exception);
            args.SetObserved();
        };

        // Tray icon (WinForms) callbacks would otherwise show the default WinForms error dialog.
        System.Windows.Forms.Application.ThreadException += (_, args) => Logger.Log(args.Exception);

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

            _settings = new SettingsStore();
            _settings.Load();
            StartupManager.MigrateLegacyEntry();

            _tray = new TrayService();
            _tray.OpenRequested += ToggleFlyout;
            _tray.SettingsFolderRequested += OpenSettingsFolder;
            _tray.ExitRequested += Shutdown;

            if (!background)
            {
                ToggleFlyout();
            }
            else
            {
                // The window is created on first use, so a background start only holds the tray icon.
                await Task.Delay(TimeSpan.FromSeconds(3));
                MemoryTrimmer.Trim();
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

    private void ToggleFlyout()
    {
        try
        {
            _window ??= new MainWindow(_settings!);
            _window.ToggleFlyout();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
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
