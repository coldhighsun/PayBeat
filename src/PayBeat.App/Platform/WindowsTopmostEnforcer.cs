using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace PayBeat.App.Platform;

/// <summary>
/// Forces a window's HWND back to the top of the topmost band via <c>SetWindowPos</c>, and watches
/// foreground-window changes through a Win32 event hook so callers react only when something
/// might have stolen the z-order instead of polling. <see cref="Window.Topmost"/> alone does not
/// survive explorer.exe re-asserting the taskbar's own z-order on click.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsTopmostEnforcer : ITopmostEnforcer
{
    /// <summary>
    /// <c>SWP_NOACTIVATE</c>: do not activate the window.
    /// </summary>
    private const uint SwpNoActivate = 0x0010;

    /// <summary>
    /// <c>SWP_NOMOVE</c>: keep the current position.
    /// </summary>
    private const uint SwpNoMove = 0x0002;

    /// <summary>
    /// <c>SWP_NOSIZE</c>: keep the current size.
    /// </summary>
    private const uint SwpNoSize = 0x0001;

    /// <summary>
    /// <c>EVENT_SYSTEM_FOREGROUND</c>: the foreground window changed.
    /// </summary>
    private const uint EventSystemForeground = 0x0003;

    /// <summary>
    /// <c>WINEVENT_OUTOFCONTEXT</c>: callbacks run on the thread that installed the hook.
    /// </summary>
    private const uint WinEventOutOfContext = 0x0000;

    /// <summary>
    /// <c>HWND_TOPMOST</c> sentinel for <c>SetWindowPos</c>.
    /// </summary>
    private static readonly IntPtr HwndTopmost = new(-1);

    /// <summary>
    /// Kept as a field so the delegate is not garbage-collected while the native hook holds a reference to it.
    /// </summary>
    private WinEventDelegate? _callback;

    /// <summary>
    /// Handle of the installed event hook, or <see cref="IntPtr.Zero"/>.
    /// </summary>
    private IntPtr _hook;

    /// <summary>
    /// Native callback signature for <c>SetWinEventHook</c>.
    /// </summary>
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    /// <inheritdoc/>
    public void ForceTopmost(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(handle.Handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    /// <inheritdoc/>
    public void Watch(Action onForegroundChanged)
    {
        _callback = (_, _, _, _, _, _, _) => onForegroundChanged();
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, WinEventOutOfContext);
    }

    /// <summary>
    /// Installs an out-of-context event hook.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    /// <summary>
    /// Removes an event hook installed with <see cref="SetWinEventHook"/>.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    /// <summary>
    /// Changes the size, position and z-order of a window.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
