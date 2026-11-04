using System;
using System.IO;
using System.Threading;

namespace PieceOfCake.Services;

/// <summary>
/// Appends errors to <c>error.log</c>. The file is capped: when it reaches <see cref="MaxLogBytes"/> it becomes
/// <c>error.log.old</c> (replacing the previous backup), so the log never grows beyond about twice that size.
/// </summary>
internal static class Logger
{
    internal const long MaxLogBytes = 512 * 1024;

    private static readonly Lock Gate = new();

    public static void Log(Exception exception)
    {
        Append(Path.Combine(SettingsStore.DirectoryPath, "error.log"), exception.ToString(), MaxLogBytes);
    }

    internal static void Append(string path, string message, long maxBytes)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                RotateIfNeeded(path, maxBytes);
                File.AppendAllText(path, $"[{DateTime.Now:s}] {message}{Environment.NewLine}{Environment.NewLine}");
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

    private static void RotateIfNeeded(string path, long maxBytes)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.Length >= maxBytes)
        {
            File.Move(path, path + ".old", overwrite: true);
        }
    }
}
