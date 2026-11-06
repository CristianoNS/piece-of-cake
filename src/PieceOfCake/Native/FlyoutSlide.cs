using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PieceOfCake.Native;

/// <summary>
/// Slides the flyout window up from behind the taskbar and back down, like the Windows 11 quick settings.
/// Measured on the real panel: 240 ms with a fast start and a soft landing when opening, 150 ms
/// accelerating when closing, no fade. A background thread paces the frames with the desktop compositor
/// (DwmFlush) and moves the window with an asynchronous SetWindowPos, so the motion does not depend on how busy
/// the WPF render loop is. The window is kept just below the taskbar in the z-order, so the part that is still
/// "inside" the taskbar stays hidden.
/// </summary>
internal sealed class FlyoutSlide
{
    private static readonly TimeSpan OpenTime = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan CloseTime = TimeSpan.FromMilliseconds(150);

    private readonly Window _window;

    private IntPtr _hwnd;
    private int _x;
    private int _finalY;
    private double _distance;
    private double _current;
    private int _generation;
    private Action? _completed;

    public FlyoutSlide(Window window)
    {
        _window = window;
    }

    public bool IsRunning { get; private set; }

    /// <summary>False when the user switched off animation effects in Windows.</summary>
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>
    /// Places the window below the visible area (behind the taskbar) above its final position, in device pixels.
    /// <paramref name="finalLeft"/> and <paramref name="finalTop"/> are in device-independent units.
    /// </summary>
    public void PrepareOpen(double finalLeft, double finalTop)
    {
        _hwnd = new WindowInteropHelper(_window).Handle;
        var scale = VisualTreeHelper.GetDpi(_window).DpiScaleY;
        _x = (int)Math.Round(finalLeft * scale);
        _finalY = (int)Math.Round(finalTop * scale);
        _distance = (_window.ActualHeight + 12) * scale;
        Volatile.Write(ref _current, _distance);
        Place(_distance, async: false);
        KeepBehindTaskbar();
    }

    public void Open(Action completed) => Start(opening: true, completed);

    public void Close(Action completed)
    {
        if (!IsRunning)
        {
            // Not sliding: start from the resting position.
            _hwnd = new WindowInteropHelper(_window).Handle;
            var scale = VisualTreeHelper.GetDpi(_window).DpiScaleY;
            _x = (int)Math.Round(_window.Left * scale);
            _finalY = (int)Math.Round(_window.Top * scale);
            _distance = (_window.ActualHeight + 12) * scale;
            Volatile.Write(ref _current, 0.0);
        }

        Start(opening: false, completed);
    }

    /// <summary>Stops a running slide without calling its completion callback.</summary>
    public void Cancel()
    {
        _ = Interlocked.Increment(ref _generation);
        IsRunning = false;
        _completed = null;
    }

    /// <summary>Puts the window directly under the taskbar in the topmost band (call again after activating).</summary>
    public void KeepBehindTaskbar()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (hwnd == IntPtr.Zero || taskbar == IntPtr.Zero)
        {
            return;
        }

        const uint flags = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate;
        _ = NativeMethods.SetWindowPos(hwnd, taskbar, 0, 0, 0, 0, flags);
    }

    private static double CubicBezier(double x1, double y1, double x2, double y2, double x)
    {
        // Solve x(t) = x for t by bisection, then return y(t).
        double lo = 0;
        double hi = 1;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2;
            if (Coordinate(x1, x2, mid) < x)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return Coordinate(y1, y2, (lo + hi) / 2);

        static double Coordinate(double p1, double p2, double t)
        {
            var u = 1 - t;
            return (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t);
        }
    }

    private void Start(bool opening, Action completed)
    {
        Cancel();
        var generation = _generation;
        var from = Volatile.Read(ref _current);
        var to = opening ? 0.0 : _distance;
        var duration = opening ? OpenTime : CloseTime;
        _completed = completed;
        IsRunning = true;

        var thread = new Thread(() => Run(generation, opening, from, to, duration)) { IsBackground = true };
        thread.Start();
    }

    private void Run(int generation, bool opening, double from, double to, TimeSpan duration)
    {
        var clock = Stopwatch.StartNew();
        while (generation == Volatile.Read(ref _generation))
        {
            // Wait for the next compositor frame; fall back to a short sleep when DWM is not available.
            if (NativeMethods.DwmFlush() != 0)
            {
                Thread.Sleep(8);
            }

            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }

            var progress = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / duration.TotalMilliseconds);

            // Curves fitted to frames recorded from the real quick settings panel (rms error 1 to 3 px).
            var eased = opening
                ? CubicBezier(0.0, 0.9, 0.7, 1.0, progress)
                : CubicBezier(0.2, 0.0, 0.9, 0.1, progress);

            var displacement = from + ((to - from) * eased);
            Volatile.Write(ref _current, displacement);
            Place(displacement, async: true);

            if (progress >= 1.0)
            {
                _ = _window.Dispatcher.BeginInvoke(() =>
                {
                    if (generation == Volatile.Read(ref _generation))
                    {
                        IsRunning = false;
                        var done = _completed;
                        _completed = null;
                        done?.Invoke();
                    }
                });
                return;
            }
        }
    }

    private void Place(double displacement, bool async)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var flags = NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate;
        if (async)
        {
            flags |= NativeMethods.SwpAsyncWindowPos;
        }

        _ = NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, _x, _finalY + (int)Math.Round(displacement), 0, 0, flags);
    }
}
