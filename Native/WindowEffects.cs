using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PieceOfCake.Native;

internal static class WindowEffects
{
    private const int MinBuildForBackdrop = 22621; // Windows 11 22H2

    /// <summary>
    /// Applies the Windows 11 flyout look (rounded corners, dark mode, acrylic backdrop) and hides
    /// the window from Alt+Tab. Returns true when the acrylic backdrop is active, so the caller can
    /// make the window background transparent. On older systems everything is skipped gracefully.
    /// </summary>
    public static bool ApplyFlyoutLook(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlExStyle, exStyle | NativeMethods.WsExToolWindow);

            var dark = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

            if (Environment.OSVersion.Version.Build < MinBuildForBackdrop)
            {
                return false;
            }

            var corner = NativeMethods.DwmwcpRound;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaWindowCornerPreference, ref corner, sizeof(int));

            var backdrop = NativeMethods.DwmsbtTransientWindow;
            var result = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaSystemBackdropType, ref backdrop, sizeof(int));
            if (result != 0)
            {
                return false;
            }

            var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            if (NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0)
            {
                return false;
            }

            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget == null)
            {
                return false;
            }

            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
