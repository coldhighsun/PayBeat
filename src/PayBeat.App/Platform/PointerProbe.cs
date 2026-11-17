using System.Runtime.InteropServices;
using Avalonia;

namespace PayBeat.App.Platform;

/// <summary>
/// Asks the operating system where the pointer is. Pointer enter/leave events stop being reliable after a
/// drag with pointer capture, so auto-hide uses this to decide whether the pointer is still on the window.
/// Only Windows tells; elsewhere the position is unknown and callers fall back to pointer events.
/// </summary>
internal static class PointerProbe
{
    /// <summary>
    /// Gets the pointer position in screen pixels.
    /// </summary>
    /// <param name="position">The position in screen pixels.</param>
    /// <returns><see langword="true"/> when the position is known.</returns>
    public static bool TryGetPosition(out PixelPoint position)
    {
        if (OperatingSystem.IsWindows() && GetCursorPos(out var point))
        {
            position = new PixelPoint(point.X, point.Y);
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>
    /// Win32 <c>GetCursorPos</c>.
    /// </summary>
    /// <param name="point">Receives the cursor position in screen coordinates.</param>
    /// <returns>Whether the call succeeded.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    /// <summary>
    /// Win32 <c>POINT</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        /// <summary>
        /// Horizontal coordinate.
        /// </summary>
        public int X;

        /// <summary>
        /// Vertical coordinate.
        /// </summary>
        public int Y;
    }
}
