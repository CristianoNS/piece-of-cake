using PieceOfCake.Native;

namespace PieceOfCake.Services;

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
