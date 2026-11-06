using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PieceOfCake.Services;

namespace PieceOfCake.Native;

/// <summary>
/// Gets the real icon of a program from the Windows shell, the same one the Start menu shows. It works for files,
/// shortcuts, web addresses and any app id of the form <c>shell:AppsFolder\AppId</c> (including Store apps).
/// </summary>
internal static class ShellIcon
{
    private const int IconPixels = 64;

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Load(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        if (!Cache.TryGetValue(target, out var image))
        {
            image = Create(target);
            Cache[target] = image;
        }

        return image;
    }

    private static BitmapSource? Create(string target)
    {
        NativeMethods.IShellItemImageFactory? factory = null;
        var bitmap = IntPtr.Zero;

        try
        {
            var iid = typeof(NativeMethods.IShellItemImageFactory).GUID;
            NativeMethods.SHCreateItemFromParsingName(target, IntPtr.Zero, ref iid, out factory);

            var size = new NativeMethods.ImageSize { Width = IconPixels, Height = IconPixels };
            var flags = NativeMethods.SiigbfIconOnly | NativeMethods.SiigbfBiggerSizeOk;
            if (factory.GetImage(size, flags, out bitmap) != 0 || bitmap == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidCastException)
        {
            Logger.Log(ex);
            return null;
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                _ = NativeMethods.DeleteObject(bitmap);
            }

            if (factory != null)
            {
                _ = Marshal.ReleaseComObject(factory);
            }
        }
    }
}
