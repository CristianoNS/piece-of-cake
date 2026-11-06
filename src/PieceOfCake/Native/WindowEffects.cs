using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PieceOfCake.Native;

internal static class WindowEffects
{
    private const int MinBuildForBackdrop = 22621; // Windows 11 22H2

    /// <summary>
    /// Asks DWM for the acrylic backdrop again. After the window has been hidden DWM can drop the backdrop,
    /// and the next opening then shows a flat grey panel until the attribute is set once more.
    /// </summary>
    public static void RefreshBackdrop(Window window, bool dark, bool transparency)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var darkValue = dark ? 1 : 0;
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, ref darkValue, sizeof(int));

            if (Environment.OSVersion.Version.Build < MinBuildForBackdrop)
            {
                return;
            }

            var none = NativeMethods.DwmsbtNone;
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaSystemBackdropType, ref none, sizeof(int));

            if (!transparency)
            {
                // Transparency effects are off in Windows: no backdrop, the window paints a solid colour itself.
                var noMargins = default(NativeMethods.Margins);
                _ = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref noMargins);
                return;
            }

            var backdrop = NativeMethods.DwmsbtTransientWindow;
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaSystemBackdropType, ref backdrop, sizeof(int));

            var off = default(NativeMethods.Margins);
            _ = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref off);
            var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            _ = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Tell DWM the window is active and redo its frame, otherwise it keeps the "inactive" grey fallback.
            _ = NativeMethods.SendMessage(hwnd, NativeMethods.WmNcActivate, new IntPtr(1), IntPtr.Zero);
            _ = NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, NativeMethods.SwpFrameChangedFlags);
        }
        catch (DllNotFoundException)
        {
            // Older systems: nothing to refresh.
        }
        catch (EntryPointNotFoundException)
        {
            // Older systems: nothing to refresh.
        }
    }

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
            _ = NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlExStyle, exStyle | NativeMethods.WsExToolWindow);

            if (Environment.OSVersion.Version.Build < MinBuildForBackdrop)
            {
                return false;
            }

            var corner = NativeMethods.DwmwcpRound;
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaWindowCornerPreference, ref corner, sizeof(int));

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
