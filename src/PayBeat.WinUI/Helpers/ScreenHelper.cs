using System.Runtime.InteropServices;
using Windows.Graphics;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Win32 P/Invoke helpers for multi-monitor awareness. Handles DPI-aware position restore
/// (match by device name, fall back to nearest monitor) and window clamping to the screen work
/// area. Unlike WPF, WinUI3's <c>AppWindow</c> positions and sizes are in raw physical pixels,
/// so all bounds here are physical pixels - no DPI scaling matrix is involved for the window
/// itself. <see cref="WpfUnitsToPixels"/> exists only to translate positions saved by the legacy
/// WPF build of PayBeat (which stored <c>Window.Left</c>/<c>Top</c> in 96-DPI logical units).
/// </summary>
internal static class ScreenHelper
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref Rect lprcMonitor, nint dwData);

    /// <summary>
    /// Finds the best monitor bounds (physical pixels) for restoring a saved position.
    /// Matches by device name first; falls back to the nearest monitor to the saved point.
    /// Uses <c>rcMonitor</c> (full screen including taskbar) so the window may overlap the taskbar.
    /// </summary>
    /// <param name="pixelX">Saved X position in physical pixels.</param>
    /// <param name="pixelY">Saved Y position in physical pixels.</param>
    /// <param name="deviceName">Win32 device name of the monitor the position was saved on, if known.</param>
    public static RectInt32 FindMonitorBoundsForRestore(int pixelX, int pixelY, string? deviceName)
    {
        if (deviceName != null)
        {
            var hByName = FindMonitorByDeviceName(deviceName);
            if (hByName != 0)
            {
                return GetMonitorBounds(hByName);
            }
        }

        var hNearest = MonitorFromPoint(new() { X = pixelX, Y = pixelY }, MonitorDefaultToNearest);
        return GetMonitorBounds(hNearest);
    }

    /// <summary>
    /// Returns the full bounds (physical pixels) of the monitor <paramref name="hwnd"/> is currently on.
    /// Uses <c>rcMonitor</c> (full screen including taskbar) so the window may overlap the taskbar.
    /// </summary>
    public static RectInt32 GetCurrentMonitorBounds(nint hwnd)
    {
        var hMonitor = hwnd != 0
            ? MonitorFromWindow(hwnd, MonitorDefaultToNearest)
            : GetPrimaryMonitor();

        return GetMonitorBounds(hMonitor);
    }

    /// <summary>
    /// Returns the device name of the monitor <paramref name="hwnd"/> is currently on, or
    /// <see langword="null"/> if the window has no handle yet.
    /// </summary>
    public static string? GetCurrentMonitorDeviceName(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        var hMonitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        return GetDeviceName(hMonitor);
    }

    /// <summary>
    /// Returns the current cursor position in physical screen pixels. Used for manual
    /// drag-to-move: <c>PointerRoutedEventArgs.GetCurrentPoint(null)</c> is relative to the
    /// window's own client area (which is itself being moved during the drag), not the screen,
    /// so tracking XAML pointer positions directly under-reports the drag distance as the
    /// window's coordinate frame shifts under the cursor mid-gesture.
    /// </summary>
    public static PointInt32 GetCursorScreenPosition()
    {
        GetCursorPos(out var pt);
        return new(pt.X, pt.Y);
    }

    /// <summary>
    /// Returns the DPI (96 = 100%) that Windows currently assigns to <paramref name="hwnd"/>,
    /// updated live as the window is dragged between monitors with different scale factors.
    /// </summary>
    public static uint GetDpiForWindow(nint hwnd) => GetDpiForWindowNative(hwnd);

    /// <summary>
    /// Converts a position saved in WPF's 96-DPI logical units (as the legacy WPF build stored
    /// <c>Window.Left</c>/<c>Top</c>) into physical pixels at the given DPI, for use with
    /// <c>AppWindow</c> positioning.
    /// </summary>
    public static PointInt32 WpfUnitsToPixels(double left, double top, uint dpi)
    {
        var scale = dpi / 96.0;
        return new((int)(left * scale), (int)(top * scale));
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    private static nint FindMonitorByDeviceName(string deviceName)
    {
        nint result = 0;
        EnumDisplayMonitors(0, 0,
            (hMon, _, ref _, _) =>
            {
                if (GetDeviceName(hMon) == deviceName)
                {
                    result = hMon;
                    return false;
                }
                return true;
            },
            0);
        return result;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    private static string? GetDeviceName(nint hMonitor)
    {
        var info = GetMonitorInfoEx(hMonitor);
        return string.IsNullOrEmpty(info.szDevice) ? null : info.szDevice;
    }

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindowNative(nint hWnd);

    private static RectInt32 GetMonitorBounds(nint hMonitor)
    {
        var r = GetMonitorInfoEx(hMonitor).rcMonitor;
        return new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

    private static MonitorInfoEx GetMonitorInfoEx(nint hMonitor)
    {
        var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
        GetMonitorInfo(hMonitor, ref info);
        return info;
    }

    private static nint GetPrimaryMonitor() => MonitorFromPoint(new() { X = 0, Y = 0 }, MonitorDefaultToNearest);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
}