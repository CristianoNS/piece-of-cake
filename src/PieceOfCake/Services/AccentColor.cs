using System;
using System.IO;
using System.Security;
using System.Windows.Media;
using Microsoft.Win32;

namespace PieceOfCake.Services;

/// <summary>Reads the Windows accent colour the same way the system flyouts do (the "light 2" shade in dark mode).</summary>
internal static class AccentColor
{
    private const int LightTwoOffset = 4; // AccentPalette holds 8 RGBA colours; index 1 is "light 2".

    private static readonly Color Fallback = Color.FromRgb(0x4C, 0xC2, 0xFF);

    public static Color Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= LightTwoOffset + 3)
            {
                return Color.FromRgb(palette[LightTwoOffset], palette[LightTwoOffset + 1], palette[LightTwoOffset + 2]);
            }
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            Logger.Log(ex);
        }

        return Fallback;
    }
}
