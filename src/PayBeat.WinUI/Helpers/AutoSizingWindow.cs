using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Base class for WinUI windows that size themselves to fit their content, replacing WPF's
/// declarative <c>SizeToContent</c> (no <c>AppWindow</c> equivalent exists). Subclasses call
/// <see cref="ResizeToContent"/> whenever content that affects natural size changes - initial
/// load, language switch, display-mode switch, a visibility-toggled row, a validation message,
/// etc. There is deliberately no automatic trigger (e.g. <c>LayoutUpdated</c>): that event fires
/// on every layout pass including the one caused by <c>AppWindow.Resize</c> itself, which can
/// spin the UI thread in a tight resize-triggers-layout-triggers-resize loop.
/// </summary>
public abstract class AutoSizingWindow : Window
{
    private readonly nint _hwnd;

    protected AutoSizingWindow()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
    }

    /// <summary>The window's HWND, computed once via <c>WindowNative</c> so subclasses don't each redo it.</summary>
    protected nint Hwnd => _hwnd;

    /// <summary>
    /// Measures <paramref name="rootGrid"/>, scales the result by <c>XamlRoot.RasterizationScale</c>
    /// to convert effective pixels to the physical pixels <c>AppWindow</c> expects, and resizes -
    /// optionally re-centering on the current monitor - only if the computed size differs from the
    /// window's current size, so callers can invoke this from frequent change notifications without
    /// risking redundant native resize calls.
    /// </summary>
    protected void ResizeToContent(Grid rootGrid, bool centerOnScreen = false, int? maxHeight = null)
    {
        rootGrid.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
        var desired = rootGrid.DesiredSize;
        var scale = ScreenHelper.GetDpiForWindow(_hwnd) / 96.0;

        var width = (int)Math.Ceiling(desired.Width * scale);
        var height = (int)Math.Ceiling(desired.Height * scale);
        if (maxHeight is { } cap)
        {
            height = Math.Min(height, cap);
        }

        if (width <= 0 || height <= 0 || (width == AppWindow.ClientSize.Width && height == AppWindow.ClientSize.Height))
        {
            return;
        }

        // Resize the client area rather than the whole AppWindow: when the window has a native
        // border (e.g. SettingsWindow's SetBorderAndTitleBar(true, false)), AppWindow.Resize/
        // MoveAndResize size the outer frame, leaving a client area a few pixels narrower than
        // requested and clipping the rightmost content (observed as the ScrollViewer's vertical
        // scrollbar being cut off at the window edge).
        AppWindow.ResizeClient(new(width, height));

        if (centerOnScreen)
        {
            var bounds = ScreenHelper.GetCurrentMonitorBounds(_hwnd);
            var actualSize = AppWindow.Size;
            var x = bounds.X + (bounds.Width - actualSize.Width) / 2;
            var y = bounds.Y + (bounds.Height - actualSize.Height) / 2;
            AppWindow.Move(new(x, y));
        }
    }
}
