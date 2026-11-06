using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace PieceOfCake.Services;

/// <summary>The Windows personalization settings the flyout follows (read only, never changed).</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SystemTheme(bool IsLight, bool TransparencyEnabled)
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static SystemTheme Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);

            // The taskbar and its flyouts follow "system" mode (SystemUsesLightTheme), not the apps mode.
            var light = key?.GetValue("SystemUsesLightTheme") is int systemLight && systemLight != 0;
            var transparency = key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
            return new SystemTheme(light, transparency);
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            Logger.Log(ex);
            return new SystemTheme(false, true);
        }
    }
}
