using System.Runtime.InteropServices;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Watches for foreground-window changes (e.g. clicking the taskbar or another app) via a
/// low-level Win32 event hook, so callers can react only when something might have stolen
/// the z-order, instead of polling on a timer.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    // Kept as a field so the delegate is not garbage-collected while the native hook holds a
    // reference to it.
    private readonly WinEventDelegate _callback;

    private readonly nint _hook;

    public ForegroundWatcher(Action onForegroundChanged)
    {
        _callback = (_, _, _, _, _, _, _) => onForegroundChanged();
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, _callback, 0, 0, WinEventOutOfContext);
    }

    private delegate void WinEventDelegate(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWinEvent(_hook);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint hWinEventHook);
}