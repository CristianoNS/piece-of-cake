using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PieceOfCake.Services;

public sealed class ScriptEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
}

public sealed class AppSettings
{
    public string BridgeIp { get; set; } = "";
    public string BridgeCertThumbprint { get; set; } = "";
    public string ProtectedAppKey { get; set; } = "";
    public List<ScriptEntry> Scripts { get; set; } = new();
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PieceOfCake");

    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    // Settings location used before the app was renamed to "Piece of Cake".
    private static string LegacyFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HuePanel", "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        Directory.CreateDirectory(DirectoryPath);

        if (!File.Exists(FilePath) && File.Exists(LegacyFilePath))
        {
            try
            {
                File.Copy(LegacyFilePath, FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Log(ex);
            }
        }

        if (!File.Exists(FilePath))
        {
            Current = new AppSettings();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Current.Scripts ??= new List<ScriptEntry>();
        }
        catch (JsonException ex)
        {
            Logger.Log(ex);
            var backup = Path.Combine(DirectoryPath, "settings.broken.json");
            File.Copy(FilePath, backup, overwrite: true);
            Current = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, FilePath, overwrite: true);
    }

    public string? GetAppKey()
    {
        if (string.IsNullOrEmpty(Current.ProtectedAppKey))
        {
            return null;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(Current.ProtectedAppKey), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public void SetAppKey(string? key)
    {
        Current.ProtectedAppKey = string.IsNullOrEmpty(key)
            ? ""
            : Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
    }
}
