using System;
using PieceOfCake.Native;

namespace PieceOfCake.Services;

/// <summary>Computes total CPU load from two consecutive GetSystemTimes samples.</summary>
public sealed class CpuMeter
{
    private long _lastIdle;
    private long _lastKernel;
    private long _lastUser;
    private bool _hasSample;

    public CpuMeter()
    {
        Sample();
    }

    /// <summary>Returns CPU usage in percent since the previous call (0 on the very first call).</summary>
    public double Sample()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        if (!_hasSample)
        {
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            _hasSample = true;
            return 0;
        }

        var idleDelta = idle - _lastIdle;
        var totalDelta = (kernel - _lastKernel) + (user - _lastUser);
        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;

        if (totalDelta <= 0)
        {
            return 0;
        }

        return Math.Clamp((1.0 - (double)idleDelta / totalDelta) * 100.0, 0.0, 100.0);
    }
}

public static class MemoryMeter
{
    public static bool TryGet(out double totalBytes, out double usedBytes)
    {
        totalBytes = 0;
        usedBytes = 0;

        var status = new NativeMethods.MemoryStatusEx
        {
            Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MemoryStatusEx>(),
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return false;
        }

        totalBytes = status.TotalPhys;
        usedBytes = status.TotalPhys - status.AvailPhys;
        return true;
    }
}
