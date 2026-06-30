using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace PayBeat.App.Helpers;

/// <summary>
/// Win32 P/Invoke helpers for multi-monitor awareness. Handles DPI-aware position restore
/// (match by device name, fall back to nearest monitor) and window clamping to screen work area.
/// </summary>
internal static class ScreenHelper
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    /// <summary>
    /// Finds the best screen bounds (in WPF logical units) for restoring a saved position.
    /// Matches by device name first; falls back to the nearest monitor to the saved position.
    /// Uses rcMonitor (full screen including taskbar) so the window may overlap the taskbar.
    /// </summary>
    public static Rect FindScreenBoundsForRestore(double wpfLeft, double wpfTop, string? deviceName, Visual visual)
    {
        if (deviceName != null)
        {
            var hByName = FindMonitorByDeviceName(deviceName);
            if (hByName != IntPtr.Zero)
            {
                return GetScreenBoundsWpf(hByName, visual);
            }
        }

        var dpi = GetDpiScale(visual);
        var pt = new POINT
        {
            X = (int)(wpfLeft * dpi.X),
            Y = (int)(wpfTop * dpi.Y)
        };
        var hNearest = MonitorFromPoint(pt, MonitorDefaultToNearest);
        return GetScreenBoundsWpf(hNearest, visual);
    }

    /// <summary>
    /// Returns the full bounds (in WPF logical units) of the monitor the WPF window is on.
    /// Uses rcMonitor (full screen including taskbar) so the window may overlap the taskbar.
    /// </summary>
    public static Rect GetCurrentScreenBounds(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var hMonitor = hwnd != IntPtr.Zero
            ? MonitorFromWindow(hwnd, MonitorDefaultToNearest)
            : GetPrimaryMonitor();

        return GetScreenBoundsWpf(hMonitor, window);
    }

    /// <summary>
    /// Returns the device name of the monitor the WPF window is on.
    /// </summary>
    public static string? GetCurrentScreenDeviceName(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        var hMonitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        return GetDeviceName(hMonitor);
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    private static IntPtr FindMonitorByDeviceName(string deviceName)
    {
        IntPtr result = IntPtr.Zero;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (hMon, _, ref _, _) =>
            {
                if (GetDeviceName(hMon) == deviceName)
                {
                    result = hMon;
                    return false;
                }
                return true;
            },
            IntPtr.Zero);
        return result;
    }

    private static string? GetDeviceName(IntPtr hMonitor)
    {
        var info = GetMonitorInfoEx(hMonitor);
        return string.IsNullOrEmpty(info.szDevice) ? null : info.szDevice;
    }

    private static (double X, double Y) GetDpiScale(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget != null)
        {
            var m = source.CompositionTarget.TransformToDevice;
            return (m.M11, m.M22);
        }

        return (1.0, 1.0);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    private static MONITORINFOEX GetMonitorInfoEx(IntPtr hMonitor)
    {
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        GetMonitorInfo(hMonitor, ref info);
        return info;
    }

    private static IntPtr GetPrimaryMonitor()
    {
        return MonitorFromPoint(new POINT { X = 0, Y = 0 }, MonitorDefaultToNearest);
    }

    private static Rect GetScreenBoundsWpf(IntPtr hMonitor, Visual visual)
    {
        var info = GetMonitorInfoEx(hMonitor);
        var dpi = GetDpiScale(visual);
        var r = info.rcMonitor;
        return new Rect(
            r.Left / dpi.X,
            r.Top / dpi.Y,
            (r.Right - r.Left) / dpi.X,
            (r.Bottom - r.Top) / dpi.Y);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}