using System.Runtime.InteropServices;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Forces a window's HWND back to the top of the topmost band via <c>SetWindowPos</c>.
/// <c>OverlappedPresenter.IsAlwaysOnTop</c> alone does not survive explorer.exe re-asserting
/// the taskbar's own z-order on click, so callers must periodically re-apply this.
/// </summary>
internal static class TopmostHelper
{
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private static readonly nint HwndTopmost = -1;

    /// <summary>
    /// Re-applies <c>HWND_TOPMOST</c> to the window without moving, resizing, or activating it.
    /// No-ops if <paramref name="hwnd"/> is zero.
    /// </summary>
    public static void ForceTopmost(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
