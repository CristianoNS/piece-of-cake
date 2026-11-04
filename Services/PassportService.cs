using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.Win32;

namespace PieceOfCake.Services;

/// <summary>Builds a Markdown "passport" of this computer: hardware, system, disks, network and installed programs.</summary>
public static class PassportService
{
    public static string CreateAndSave()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PieceOfCake");
        Directory.CreateDirectory(directory);

        var machine = new string(Environment.MachineName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (machine.Length == 0)
        {
            machine = "pc";
        }

        var path = Path.Combine(directory, $"computer-passport-{machine}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, Build(), new UTF8Encoding(false));
        return path;
    }

    private static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Paszport komputera: {Environment.MachineName}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        AppendSystem(sb);
        AppendDisks(sb);
        AppendNetwork(sb);
        AppendPrograms(sb);

        return sb.ToString();
    }

    private static void AppendSystem(StringBuilder sb)
    {
        string product = "Windows";
        string version = "";
        string build = "";

        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            product = key?.GetValue("ProductName") as string ?? product;
            version = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "";
            build = key?.GetValue("CurrentBuildNumber") as string ?? "";
            var ubr = key?.GetValue("UBR");
            if (ubr != null && build.Length > 0)
            {
                build += "." + ubr;
            }
        }

        // Windows 11 still reports "Windows 10" in ProductName.
        if (int.TryParse(build.Split('.')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var buildNumber) && buildNumber >= 22000)
        {
            product = product.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
        }

        string cpu;
        using (var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
        {
            cpu = (cpuKey?.GetValue("ProcessorNameString") as string)?.Trim() ?? "nieznany";
        }

        var ram = MemoryMeter.TryGet(out var total, out _) ? FormatBytes(total) : "nieznana";

        sb.AppendLine("## System i sprzęt");
        sb.AppendLine();
        sb.AppendLine("| Pozycja | Wartość |");
        sb.AppendLine("|---|---|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Nazwa komputera | {Cell(Environment.MachineName)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Użytkownik | {Cell(Environment.UserName)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| System | {Cell(product)} {Cell(version)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Kompilacja | {Cell(build)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Architektura | {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Procesor | {Cell(cpu)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Wątki logiczne | {Environment.ProcessorCount} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Pamięć RAM | {ram} |");
        sb.AppendLine();
    }

    private static void AppendDisks(StringBuilder sb)
    {
        sb.AppendLine("## Dyski");
        sb.AppendLine();
        sb.AppendLine("| Dysk | Etykieta | System plików | Pojemność | Wolne |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| {Cell(drive.Name)} | {Cell(drive.VolumeLabel)} | {Cell(drive.DriveFormat)} | " +
                    $"{FormatBytes(drive.TotalSize)} | {FormatBytes(drive.TotalFreeSpace)} |");
            }
            catch (IOException)
            {
                // Drive disappeared while reading; skip it.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        sb.AppendLine();
    }

    private static void AppendNetwork(StringBuilder sb)
    {
        sb.AppendLine("## Sieć");
        sb.AppendLine();
        sb.AppendLine("| Karta | Typ | MAC | Adresy IPv4 |");
        sb.AppendLine("|---|---|---|---|");

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up
                    || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var mac = string.Join("-", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                var addresses = string.Join(
                    ", ",
                    nic.GetIPProperties().UnicastAddresses
                        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => a.Address.ToString()));

                sb.AppendLine(CultureInfo.InvariantCulture, $"| {Cell(nic.Name)} | {nic.NetworkInterfaceType} | {Cell(mac)} | {Cell(addresses)} |");
            }
        }
        catch (NetworkInformationException ex)
        {
            Logger.Log(ex);
            sb.AppendLine("| (nie udało się odczytać kart sieciowych) | | | |");
        }

        sb.AppendLine();
    }

    private static void AppendPrograms(StringBuilder sb)
    {
        var programs = new List<(string Name, string Version, string Publisher)>();
        var locations = new (RegistryKey Root, string Path)[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (root, path) in locations)
        {
            using var parent = root.OpenSubKey(path);
            if (parent == null)
            {
                continue;
            }

            foreach (var name in parent.GetSubKeyNames())
            {
                using var app = parent.OpenSubKey(name);
                var displayName = (app?.GetValue("DisplayName") as string)?.Trim();
                if (string.IsNullOrEmpty(displayName)
                    || displayName.StartsWith("KB", StringComparison.Ordinal)
                    || app?.GetValue("SystemComponent") is int and 1)
                {
                    continue;
                }

                programs.Add((
                    displayName,
                    (app?.GetValue("DisplayVersion") as string)?.Trim() ?? "",
                    (app?.GetValue("Publisher") as string)?.Trim() ?? ""));
            }
        }

        var distinct = programs
            .GroupBy(p => (p.Name.ToLowerInvariant(), p.Version))
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        sb.AppendLine(CultureInfo.InvariantCulture, $"## Zainstalowane programy ({distinct.Count})");
        sb.AppendLine();
        sb.AppendLine("| Program | Wersja | Wydawca |");
        sb.AppendLine("|---|---|---|");
        foreach (var p in distinct)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {Cell(p.Name)} | {Cell(p.Version)} | {Cell(p.Publisher)} |");
        }
    }

    private static string Cell(string? value) =>
        (value ?? "").Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static string FormatBytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var index = 0;
        while (bytes >= 1024 && index < units.Length - 1)
        {
            bytes /= 1024;
            index++;
        }

        return $"{bytes:0.#} {units[index]}";
    }
}
