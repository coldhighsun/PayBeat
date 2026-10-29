using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace PayBeat.App.Platform;

/// <summary>
/// Registers and manages a Win32 global hotkey for toggling widget visibility.
/// Supports suspending the hotkey while the settings window captures key input.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHotkeyService : IHotkeyService
{
    /// <summary>
    /// Identifier passed to <c>RegisterHotKey</c> and echoed back in <c>WM_HOTKEY</c>.
    /// </summary>
    private const int Id = 0x3001;

    /// <summary>
    /// Window message sent when a registered hotkey is pressed.
    /// </summary>
    private const uint WmHotkey = 0x0312;

    /// <summary>
    /// Native handle of the window that owns the registration, or <see cref="IntPtr.Zero"/>.
    /// </summary>
    private IntPtr _hwnd;

    /// <summary>
    /// Window whose message loop is hooked, kept so the hook can be removed on dispose.
    /// </summary>
    private Window? _window;

    /// <summary>
    /// Whether <see cref="Triggered"/> is currently suppressed.
    /// </summary>
    private bool _suspended;

    /// <inheritdoc/>
    public event Action? Triggered;

    /// <inheritdoc/>
    public bool IsSupported => true;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, Id);
            if (_window is not null)
            {
                Win32Properties.RemoveWndProcHookCallback(_window, WndProc);
            }

            _hwnd = IntPtr.Zero;
            _window = null;
        }
    }

    /// <inheritdoc/>
    public bool Register(Window window, int modifiers, int virtualKey)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return false;
        }

        _window = window;
        _hwnd = handle.Handle;
        Win32Properties.AddWndProcHookCallback(window, WndProc);
        return RegisterHotKey(_hwnd, Id, (uint)modifiers, (uint)virtualKey);
    }

    /// <inheritdoc/>
    public void Resume() => _suspended = false;

    /// <inheritdoc/>
    public void Suspend() => _suspended = true;

    /// <inheritdoc/>
    public bool Update(int modifiers, int virtualKey)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        UnregisterHotKey(_hwnd, Id);
        return RegisterHotKey(_hwnd, Id, (uint)modifiers, (uint)virtualKey);
    }

    /// <summary>
    /// Registers a system-wide hotkey for the given window.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// Removes a hotkey previously registered with <see cref="RegisterHotKey"/>.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Window-procedure hook that raises <see cref="Triggered"/> for <c>WM_HOTKEY</c>.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == Id)
        {
            if (!_suspended)
            {
                Triggered?.Invoke();
            }

            handled = true;
        }

        return IntPtr.Zero;
    }
}
