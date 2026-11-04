using System;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace PieceOfCake.Services;

/// <summary>
/// Notification-area icon with a small context menu. The glyph is white on a dark taskbar and
/// dark on a light taskbar, and is swapped automatically when the Windows theme changes.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Forms.NotifyIcon _icon;
    private bool _lightTaskbar;
    private Drawing.Size _iconSize;
    private bool _disposed;

    public TrayService()
    {
        _lightTaskbar = IsTaskbarLight();
        _iconSize = Forms.SystemInformation.SmallIconSize;

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(_lightTaskbar, _iconSize),
            Text = "Piece of Cake",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke();
            }
        };

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event Action? OpenRequested;

    public event Action? SettingsFolderRequested;

    public event Action? ExitRequested;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Otwórz panel", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Folder ustawień", null, (_, _) => SettingsFolderRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Zakończ", null, (_, _) => ExitRequested?.Invoke());
        return menu;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Window))
        {
            return;
        }

        // SystemEvents raises this on its own thread; the icon must be updated on the UI thread.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(RefreshIcon));
    }

    private void RefreshIcon()
    {
        if (_disposed)
        {
            return;
        }

        var light = IsTaskbarLight();
        var size = Forms.SystemInformation.SmallIconSize;
        if (light == _lightTaskbar && size == _iconSize)
        {
            return;
        }

        _lightTaskbar = light;
        _iconSize = size;

        var previous = _icon.Icon;
        _icon.Icon = LoadIcon(light, size);
        previous?.Dispose();
    }

    /// <summary>The taskbar and notification area follow SystemUsesLightTheme (0 = dark, 1 = light).</summary>
    private static bool IsTaskbarLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    private static Drawing.Icon LoadIcon(bool lightTaskbar, Drawing.Size size)
    {
        var resourceName = lightTaskbar ? "tray-black.ico" : "tray-white.ico";
        using var stream = typeof(TrayService).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Brak zasobu ikony: {resourceName}.");
        return new Drawing.Icon(stream, size);
    }
}
