using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PieceOfCake.Native;
using PieceOfCake.Services;
using PieceOfCake.ViewModels;

namespace PieceOfCake;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 12;
    private const double SlideDistance = 40;
    private static readonly Duration OpenDuration = new(TimeSpan.FromMilliseconds(230));

    private readonly SettingsStore _settings;
    private readonly CpuMeter _cpu = new();
    private readonly NetworkMeter _network = new();
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _statusTimer;

    private readonly ActionTileVm _refreshTile;
    private readonly ActionTileVm _passportTile;
    private readonly ActionTileVm _autostartTile;

    private SystemTheme _theme = new(false, true);
    private bool _backdropSupported;
    private bool _appliedTransparency = true;
    private bool _settingsChanged;
    private DateTime _lastHiddenUtc = DateTime.MinValue;
    private bool _opening;

    public MainWindow(SettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();

        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 2 * ScreenMargin);

        _refreshTile = new ActionTileVm("Odśwież", "", () => _ = RefreshHueAsync());
        _passportTile = new ActionTileVm("Paszport", "", () => _ = CreatePassportAsync());
        _autostartTile = new ActionTileVm("Autostart", "", ToggleAutostart);
        TilesControl.ItemsSource = new CompositeCollection
        {
            new CollectionContainer { Collection = _rooms },
            new CollectionContainer { Collection = _settings.Current.Scripts },
            new CollectionContainer { Collection = new[] { _refreshTile, _passportTile, _autostartTile } },
        };

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
            if (IsVisible && !_opening)
            {
                PositionWindow();
            }
        };

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Closed += (_, _) =>
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _pairCts?.Cancel();
            _hue?.Dispose();
        };
    }

    /// <summary>
    /// True when the Windows appearance settings changed after this window last looked at them. DWM keeps the old
    /// backdrop state of an existing window (flat grey after transparency is switched back on), so the owner
    /// should create a new window instead of reusing this one.
    /// </summary>
    public bool NeedsRecreate => _settingsChanged || SystemTheme.Read().TransparencyEnabled != _appliedTransparency;

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Window)
        {
            _settingsChanged = true;
        }
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

    /// <summary>
    /// Builds the window, its native handle and the first layout while it is invisible, so the first real
    /// opening is as fast as the later ones. Call it once after a background start.
    /// </summary>
    public void Warm()
    {
        ShowActivated = false;
        Left = -32000;
        Show();
        UpdateLayout();
        Hide();
        ShowActivated = true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _backdropSupported = WindowEffects.ApplyFlyoutLook(this);
        ApplyTheme(SystemTheme.Read());
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // The acrylic backdrop only renders for an active window; set it again once activation has happened.
        if (IsVisible)
        {
            WindowEffects.RefreshBackdrop(this, !_theme.IsLight, UsesAcrylic);
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        // Hiding inside the deactivation notification leaves the DWM acrylic backdrop in a broken state (the next
        // opening is flat grey), so let the notification finish first.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            if (!IsActive)
            {
                HideFlyout();
            }
        });
    }

    private void ShowFlyout()
    {
        _autostartTile.IsOn = StartupManager.IsEnabled();
        ApplyTheme(SystemTheme.Read());
        ShowHomePage();

        // Keep the first frame invisible and off screen until the layout is known, so nothing flashes.
        RootBorder.Opacity = 0;
        Left = -32000;
        Show();
        WindowEffects.RefreshBackdrop(this, !_theme.IsLight, UsesAcrylic);
        UpdateLayout();
        PositionWindow();
        PlayOpenAnimation();
        Activate();

        // Slow work waits until the first frame is on screen.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, StartMetrics);
        _ = RefreshAllAsync();
    }

    private void StartMetrics()
    {
        if (!IsVisible)
        {
            return;
        }

        // The adapter list is read once per opening; the timer then only reads two counters per adapter.
        _network.Refresh();
        NetText.Text = "–";
        NetUpText.Text = "–";
        NetUpIcon.Visibility = Visibility.Visible;
        _metricsTimer.Start();
    }

    private void HideFlyout()
    {
        if (!IsVisible)
        {
            return;
        }

        _metricsTimer.Stop();
        ShowHomePage();
        _opening = false;
        BeginAnimation(TopProperty, null);
        RootBorder.BeginAnimation(OpacityProperty, null);
        Hide();
        _lastHiddenUtc = DateTime.UtcNow;

        // Give memory back once the panel is hidden; low priority so the hide itself is not delayed.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, MemoryTrimmer.Trim);
    }

    /// <summary>Slides the panel up a few pixels while it fades in, like the system flyouts.</summary>
    private void PlayOpenAnimation()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var target = Top;
        _opening = true;
        Top = target + SlideDistance;

        var slide = new DoubleAnimation(target + SlideDistance, target, OpenDuration) { EasingFunction = easing };
        slide.Completed += (_, _) =>
        {
            // Drop the animation and settle on the real position (the height may have changed meanwhile).
            _opening = false;
            BeginAnimation(TopProperty, null);
            PositionWindow();
        };

        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, OpenDuration) { EasingFunction = easing });
        BeginAnimation(TopProperty, slide);
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
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (ScenesPage.Visibility == Visibility.Visible)
        {
            ShowHomePage();
        }
        else
        {
            HideFlyout();
        }
    }

    // ---------- CPU / RAM ----------

    private void UpdateNetwork()
    {
        var connected = _network.TrySample(out var download, out var upload);
        NetText.Text = connected ? NetworkMeter.FormatSpeed(download) : "brak połączenia";
        NetUpText.Text = connected ? NetworkMeter.FormatSpeed(upload) : "";
        NetUpIcon.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMetrics()
    {
        var cpu = _cpu.Sample();
        CpuText.Text = $"{cpu:0}%";

        if (MemoryMeter.TryGet(out var total, out var used) && total > 0)
        {
            const double gigabyte = 1024.0 * 1024.0 * 1024.0;
            RamText.Text = $"{used / gigabyte:0.0} GB";
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

    private async Task CreatePassportAsync()
    {
        _passportTile.IsEnabled = false;
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
            _passportTile.IsEnabled = true;
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

    private void ToggleAutostart()
    {
        var wanted = !_autostartTile.IsOn;

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

        _autostartTile.IsOn = StartupManager.IsEnabled();
    }

    private void ActionTile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ActionTileVm tile)
        {
            tile.Action();
        }
    }

    // ---------- Pages and accent ----------

    private void ShowHomePage()
    {
        ScenesPage.Visibility = Visibility.Collapsed;
        HomePage.Visibility = Visibility.Visible;
    }

    private void ShowScenesPage(HueRoomVm room)
    {
        ScenesPage.DataContext = room;
        HomePage.Visibility = Visibility.Collapsed;
        ScenesPage.Visibility = Visibility.Visible;
    }

    private void ScenesBackButton_Click(object sender, RoutedEventArgs e) => ShowHomePage();

    private bool UsesAcrylic => _backdropSupported && _theme.TransparencyEnabled;

    /// <summary>
    /// Follows the Windows settings (read only): light or dark system mode, transparency effects on or off and the
    /// accent colour. With transparency off the panel is solid, like the system flyouts.
    /// </summary>
    private void ApplyTheme(SystemTheme theme)
    {
        _theme = theme;
        _appliedTransparency = theme.TransparencyEnabled;
        ThemePalette.Apply(Application.Current.Resources, theme.IsLight, AccentColor.Read(theme.IsLight), UsesAcrylic);

        if (UsesAcrylic)
        {
            Background = Brushes.Transparent;
            RootBorder.Background = new SolidColorBrush(ThemePalette.AcrylicTint(theme.IsLight));
        }
        else
        {
            var solid = new SolidColorBrush(ThemePalette.SolidBackground(theme.IsLight));
            Background = solid;
            RootBorder.Background = solid;
        }
    }

    private void ShowFooterStatus(string text)
    {
        FooterStatus.Text = text;
        FooterStatus.Visibility = Visibility.Visible;
        _statusTimer.Stop();
        _statusTimer.Start();
    }
}
