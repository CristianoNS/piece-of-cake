using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PieceOfCake.Native;
using PieceOfCake.Services;

namespace PieceOfCake;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 12;

    private readonly SettingsStore _settings;
    private readonly CpuMeter _cpu = new();
    private readonly NetworkMeter _network = new();
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _statusTimer;

    private DateTime _lastHiddenUtc = DateTime.MinValue;

    public MainWindow(SettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();

        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 2 * ScreenMargin);

        ScriptsControl.ItemsSource = _settings.Current.Scripts;
        ScriptsHint.Visibility = _settings.Current.Scripts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HueRoomsControl.ItemsSource = _rooms;

        _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _metricsTimer.Tick += (_, _) =>
        {
            UpdateMetrics();
            UpdateNetwork();
        };

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            FooterStatus.Visibility = Visibility.Collapsed;
        };

        SizeChanged += (_, _) =>
        {
            if (IsVisible)
            {
                PositionWindow();
            }
        };

        Closed += (_, _) =>
        {
            _pairCts?.Cancel();
            _hue?.Dispose();
        };
    }

    /// <summary>Shows the flyout above the taskbar, or hides it when it is already visible.</summary>
    public void ToggleFlyout()
    {
        if (IsVisible)
        {
            HideFlyout();
            return;
        }

        // A click on the tray icon first deactivates (and hides) the flyout; ignore the follow-up click.
        if (DateTime.UtcNow - _lastHiddenUtc < TimeSpan.FromMilliseconds(300))
        {
            return;
        }

        ShowFlyout();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (WindowEffects.ApplyFlyoutLook(this))
        {
            Background = Brushes.Transparent;
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(0xB8, 0x20, 0x20, 0x20));
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        HideFlyout();
    }

    private void ShowFlyout()
    {
        AutostartToggle.IsChecked = StartupManager.IsEnabled();

        Show();
        UpdateLayout();
        PositionWindow();
        Activate();

        // The adapter list is read once per opening; the timer then only reads two counters per adapter.
        _network.Refresh();
        NetText.Text = "–";

        _metricsTimer.Start();
        _ = RefreshAllAsync();
    }

    private void HideFlyout()
    {
        if (!IsVisible)
        {
            return;
        }

        _metricsTimer.Stop();
        Hide();
        _lastHiddenUtc = DateTime.UtcNow;

        // Give memory back once the panel is hidden; low priority so the hide itself is not delayed.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, MemoryTrimmer.Trim);
    }

    private void PositionWindow()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - ScreenMargin;
        Top = area.Bottom - ActualHeight - ScreenMargin;
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            UpdateMetrics();
            await RefreshHueAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideFlyout();
        }
    }

    // ---------- CPU / RAM ----------

    private void UpdateNetwork()
    {
        NetText.Text = _network.TrySample(out var download, out var upload)
            ? $"↓ {NetworkMeter.FormatSpeed(download)}   ↑ {NetworkMeter.FormatSpeed(upload)}"
            : "brak połączenia";
    }

    private void UpdateMetrics()
    {
        var cpu = _cpu.Sample();
        CpuBar.Value = cpu;
        CpuText.Text = $"{cpu:0}%";

        if (MemoryMeter.TryGet(out var total, out var used) && total > 0)
        {
            const double gigabyte = 1024.0 * 1024.0 * 1024.0;
            RamBar.Value = used / total * 100.0;
            RamText.Text = $"{used / gigabyte:0.0} / {total / gigabyte:0.0} GB";
        }
    }

    // ---------- Scripts ----------

    private void ScriptButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ScriptEntry entry)
        {
            return;
        }

        try
        {
            if (!File.Exists(entry.Path))
            {
                ShowFooterStatus($"Nie znaleziono pliku: {entry.Path}");
                return;
            }

            var startInfo = new ProcessStartInfo(entry.Path)
            {
                UseShellExecute = true,
                Arguments = entry.Arguments,
                WorkingDirectory = Path.GetDirectoryName(entry.Path) ?? "",
            };
            Process.Start(startInfo);
            ShowFooterStatus($"Uruchomiono: {entry.Name}");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Logger.Log(ex);
            ShowFooterStatus($"Nie udało się uruchomić „{entry.Name}”: {ex.Message}");
        }
    }

    // ---------- Footer ----------

    private async void PassportButton_Click(object sender, RoutedEventArgs e)
    {
        PassportButton.IsEnabled = false;
        ShowFooterStatus("Generuję paszport komputera…");

        try
        {
            var path = await Task.Run(PassportService.CreateAndSave);
            ShowFooterStatus("Zapisano: " + Path.GetFileName(path));
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            Logger.Log(ex);
            ShowFooterStatus("Nie udało się zapisać paszportu: " + ex.Message);
        }
        finally
        {
            PassportButton.IsEnabled = true;
        }
    }

    private void SettingsFolderButton_Click(object sender, RoutedEventArgs e)
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
            ShowFooterStatus("Nie udało się otworzyć folderu ustawień.");
        }
    }

    private void AutostartToggle_Click(object sender, RoutedEventArgs e)
    {
        var wanted = AutostartToggle.IsChecked == true;

        try
        {
            StartupManager.SetEnabled(wanted);
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException)
        {
            ShowFooterStatus(ex.Message);
        }

        AutostartToggle.IsChecked = StartupManager.IsEnabled();
    }

    private void ShowFooterStatus(string text)
    {
        FooterStatus.Text = text;
        FooterStatus.Visibility = Visibility.Visible;
        _statusTimer.Stop();
        _statusTimer.Start();
    }
}
