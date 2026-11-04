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
    private const string VolumeGlyph = "";
    private const string MutedGlyph = "";

    private readonly SettingsStore _settings;
    private readonly VolumeService _volume = new();
    private readonly CpuMeter _cpu = new();
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _statusTimer;

    private DateTime _lastHiddenUtc = DateTime.MinValue;
    private bool _updatingVolume;

    public MainWindow(SettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();

        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 2 * ScreenMargin);

        ScriptsControl.ItemsSource = _settings.Current.Scripts;
        ScriptsHint.Visibility = _settings.Current.Scripts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HueRoomsControl.ItemsSource = _rooms;

        _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _metricsTimer.Tick += (_, _) => OnMetricsTick();

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
            _volume.Dispose();
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
            RefreshVolume();
            UpdateMetrics();
            await RefreshPowerPlanAsync();
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

    // ---------- Volume ----------

    private void RefreshVolume()
    {
        if (!_volume.TryGet(out var volume, out var muted))
        {
            VolumeSlider.IsEnabled = false;
            MuteButton.IsEnabled = false;
            return;
        }

        VolumeSlider.IsEnabled = true;
        MuteButton.IsEnabled = true;

        _updatingVolume = true;
        try
        {
            VolumeSlider.Value = volume * 100.0;
        }
        finally
        {
            _updatingVolume = false;
        }

        VolumeText.Text = $"{Math.Round(volume * 100.0):0}%";
        MuteButton.Content = muted ? MutedGlyph : VolumeGlyph;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || _updatingVolume)
        {
            return;
        }

        _volume.SetVolume(e.NewValue / 100.0);
        VolumeText.Text = $"{Math.Round(e.NewValue):0}%";
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_volume.TryGet(out _, out var muted))
        {
            _volume.SetMuted(!muted);
        }

        RefreshVolume();
    }

    // ---------- CPU / RAM ----------

    private void OnMetricsTick()
    {
        UpdateMetrics();

        // Pick up volume changes made elsewhere, but never fight the user's drag.
        if (!VolumeSlider.IsMouseCaptureWithin)
        {
            RefreshVolume();
        }
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

    // ---------- Power plan ----------

    private async Task RefreshPowerPlanAsync()
    {
        var active = await Task.Run(PowerPlanService.GetActive);
        PowerSaverButton.IsChecked = active == PowerPlanService.PowerSaver;
        PowerBalancedButton.IsChecked = active == PowerPlanService.Balanced;
        PowerHighButton.IsChecked = active == PowerPlanService.HighPerformance;
    }

    private async void PowerPlan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string tag } || !Guid.TryParse(tag, out var plan))
        {
            return;
        }

        var ok = await Task.Run(() => PowerPlanService.SetActive(plan));
        if (!ok)
        {
            ShowFooterStatus("Nie udało się zmienić planu zasilania (ten plan może być niedostępny na tym komputerze).");
        }

        await RefreshPowerPlanAsync();
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
