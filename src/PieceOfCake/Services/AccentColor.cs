using System;
using System.IO;
using System.Security;
using System.Windows.Media;
using Microsoft.Win32;

namespace PieceOfCake.Services;

/// <summary>Reads the Windows accent colour the same way the system flyouts use it.</summary>
internal static class AccentColor
{
    // AccentPalette holds 8 RGBA colours: index 1 is "light 2" (used in dark mode), index 4 is "dark 1"
    // (used in light mode); both measured against the real quick settings panel.
    private const int LightTwoOffset = 4;
    private const int DarkOneOffset = 16;

    public static Color Read(bool light)
    {
        var offset = light ? DarkOneOffset : LightTwoOffset;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= offset + 3)
            {
                return Color.FromRgb(palette[offset], palette[offset + 1], palette[offset + 2]);
            }
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            Logger.Log(ex);
        }

        return light ? Color.FromRgb(0x00, 0x67, 0xC0) : Color.FromRgb(0x4C, 0xC2, 0xFF);
    }
}
