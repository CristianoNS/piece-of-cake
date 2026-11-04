using System;
using System.IO;
using System.Threading;

namespace PieceOfCake.Services;

internal static class Logger
{
    private static readonly Lock Gate = new();

    public static void Log(Exception exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.DirectoryPath);
                var path = Path.Combine(SettingsStore.DirectoryPath, "error.log");
                File.AppendAllText(path, $"[{DateTime.Now:s}] {exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging must never crash the app.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
