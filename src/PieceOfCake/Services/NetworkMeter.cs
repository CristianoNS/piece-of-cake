using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace PieceOfCake.Services;

/// <summary>
/// Download and upload speed of the adapters that lead to the internet (the ones with a default gateway).
/// The adapter list is read once per <see cref="Refresh"/> (when the panel opens); a sample only reads two
/// byte counters per adapter, so it is cheap enough to run every second while the panel is visible.
/// </summary>
public sealed class NetworkMeter
{
    private static readonly string[] Units = { "B/s", "KB/s", "MB/s", "GB/s" };

    private IReadOnlyList<NetworkInterface> _adapters = Array.Empty<NetworkInterface>();
    private long _lastReceived;
    private long _lastSent;
    private long _lastTimestamp;

    /// <summary>Re-reads the adapter list and starts measuring from now.</summary>
    public void Refresh()
    {
        try
        {
            _adapters = NetworkInterface.GetAllNetworkInterfaces().Where(IsInternetAdapter).ToList();
        }
        catch (NetworkInformationException ex)
        {
            Logger.Log(ex);
            _adapters = Array.Empty<NetworkInterface>();
        }

        (_lastReceived, _lastSent) = ReadCounters();
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Speed in bytes per second since the previous call; false when there is no connection.</summary>
    public bool TrySample(out double downloadBytesPerSecond, out double uploadBytesPerSecond)
    {
        downloadBytesPerSecond = 0;
        uploadBytesPerSecond = 0;

        if (_adapters.Count == 0)
        {
            return false;
        }

        var (received, sent) = ReadCounters();
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;

        downloadBytesPerSecond = CalculateRate(_lastReceived, received, seconds);
        uploadBytesPerSecond = CalculateRate(_lastSent, sent, seconds);

        _lastReceived = received;
        _lastSent = sent;
        _lastTimestamp = now;
        return true;
    }

    internal static double CalculateRate(long previous, long current, double seconds)
    {
        // A counter that went backwards (adapter reset) or no elapsed time cannot give a meaningful speed.
        if (seconds <= 0 || current < previous)
        {
            return 0;
        }

        return (current - previous) / seconds;
    }

    internal static string FormatSpeed(double bytesPerSecond)
    {
        var value = Math.Max(0, bytesPerSecond);
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit > 0 && value < 10 ? "{0:0.0} {1}" : "{0:0} {1}";
        return string.Format(CultureInfo.CurrentCulture, format, value, Units[unit]);
    }

    private static bool IsInternetAdapter(NetworkInterface adapter)
    {
        if (adapter.OperationalStatus != OperationalStatus.Up
            || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        {
            return false;
        }

        try
        {
            return adapter.GetIPProperties().GatewayAddresses.Any(
                gateway => !gateway.Address.Equals(IPAddress.Any) && !gateway.Address.Equals(IPAddress.IPv6Any));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private (long Received, long Sent) ReadCounters()
    {
        long received = 0;
        long sent = 0;

        foreach (var adapter in _adapters)
        {
            try
            {
                var statistics = adapter.GetIPv4Statistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // The adapter disappeared since the list was read; the next refresh drops it.
            }
        }

        return (received, sent);
    }
}
