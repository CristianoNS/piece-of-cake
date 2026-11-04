using System;
using System.IO;
using Microsoft.Win32;

namespace PieceOfCake.Services;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PieceOfCake";
    private const string LegacyValueName = "HuePanel";

    /// <summary>
    /// If the app was registered for autostart under its old name, replaces that entry with the new one
    /// (otherwise Windows would keep starting the old executable).
    /// </summary>
    public static void MigrateLegacyEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(LegacyValueName) == null)
            {
                return;
            }

            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            SetEnabled(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException)
        {
            Logger.Log(ex);
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    /// <summary>Adds or removes the per-user autostart entry. Throws InvalidOperationException when not running from an .exe.</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException("Nie można otworzyć klucza autostartu w rejestrze.");
        }

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)
            || string.Equals(Path.GetFileNameWithoutExtension(exePath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Autostart działa tylko dla zbudowanego pliku PieceOfCake.exe.");
        }

        key.SetValue(ValueName, $"\"{exePath}\" --background", RegistryValueKind.String);
    }
}
