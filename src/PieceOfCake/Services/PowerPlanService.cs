using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PieceOfCake.Services;

public static class PowerPlanService
{
    public static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static Guid? GetActive()
    {
        var output = Run("/getactivescheme", out var exitCode);
        if (exitCode != 0 || output == null)
        {
            return null;
        }

        var match = GuidPattern.Match(output);
        return match.Success && Guid.TryParse(match.Value, out var id) ? id : null;
    }

    public static bool SetActive(Guid id)
    {
        Run("/setactive " + id.ToString("D"), out var exitCode);
        return exitCode == 0;
    }

    private static string? Run(string arguments, out int exitCode)
    {
        exitCode = -1;

        try
        {
            var startInfo = new ProcessStartInfo("powercfg.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            if (!process.WaitForExit(4000))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Process already finished.
                }

                return null;
            }

            exitCode = process.ExitCode;
            return process.StandardOutput.ReadToEnd();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Logger.Log(ex);
            return null;
        }
    }
}
