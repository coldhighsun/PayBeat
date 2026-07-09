using System.Runtime.InteropServices;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Subclasses a top-level window's WndProc so multiple independent listeners (hotkey, drag
/// hit-testing, key interception) can observe raw Win32 messages without each needing its own
/// <c>HwndSource</c>-style hook, which WinUI3 does not expose. Unhandled messages are forwarded
/// to the original WndProc via <c>CallWindowProc</c>.
/// </summary>
internal sealed class WindowSubclassHelper : IDisposable
{
    private const int GwlpWndProc = -4;

    private readonly nint _hwnd;

    private readonly nint _originalWndProc;

    /// <summary>
    /// Kept as a field so the delegate is not garbage-collected while the native window still
    /// holds a function pointer to it.
    /// </summary>
    private readonly WndProcDelegate _wndProc;

    public WindowSubclassHelper(nint hwnd)
    {
        _hwnd = hwnd;
        _wndProc = WndProc;
        _originalWndProc = SetWindowLongPtr(_hwnd, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    public delegate nint? WndProcHandler(uint msg, nuint wParam, nint lParam);

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nuint wParam, nint lParam);

    /// <summary>
    /// Raised for every message the window receives, before the original WndProc sees it.
    /// A listener that fully handles a message should return a non-null result to short-circuit
    /// default processing; otherwise the message is forwarded unchanged.
    /// </summary>
    public event WndProcHandler? MessageReceived;

    public void Dispose()
    {
        if (_originalWndProc != 0)
        {
            SetWindowLongPtr(_hwnd, GwlpWndProc, _originalWndProc);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    private nint WndProc(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        // Invoke() on a multicast delegate with a non-void return only surfaces the last
        // subscriber's result, silently discarding earlier ones - iterate explicitly instead so
        // the first listener that handles the message wins.
        if (MessageReceived is { } handlers)
        {
            foreach (var @delegate in handlers.GetInvocationList())
            {
                var handler = (WndProcHandler)@delegate;
                if (handler(msg, wParam, lParam) is { } handled)
                {
                    return handled;
                }
            }
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }
}