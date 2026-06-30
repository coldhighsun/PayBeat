using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PayBeat.App.Services;

/// <summary>
/// Registers and manages a Win32 global hotkey for toggling widget visibility.
/// Supports suspending the hotkey while the settings window captures key input.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int Id = 0x3001;
    private const int WmHotkey = 0x0312;
    private IntPtr _hwnd;

    private HwndSource? _source;

    private bool _suspended;

    /// <summary>Raised on the UI thread when the hotkey is pressed and not suspended.</summary>
    public event Action? Triggered;

    /// <summary>
    /// Returns a human-readable string for a modifier + virtual-key combination (e.g. <c>Ctrl+Alt+X</c>).
    /// </summary>
    /// <param name="modifiers">Win32 modifier flags (MOD_ALT=0x01, MOD_CONTROL=0x02, MOD_SHIFT=0x04, MOD_WIN=0x08).</param>
    /// <param name="virtualKey">Virtual-key code.</param>
    public static string Format(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & 0x0002) != 0)
        {
            parts.Add("Ctrl");
        }
        if ((modifiers & 0x0001) != 0)
        {
            parts.Add("Alt");
        }
        if ((modifiers & 0x0004) != 0)
        {
            parts.Add("Shift");
        }
        if ((modifiers & 0x0008) != 0)
        {
            parts.Add("Win");
        }
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, Id);
            _source?.RemoveHook(WndProc);
            _hwnd = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Attaches a <see cref="WndProc"/> hook to <paramref name="window"/> and registers the hotkey.
    /// Must be called after the window's HWND is created (i.e. from <c>SourceInitialized</c>).
    /// </summary>
    /// <param name="window">The window whose message loop will receive the hotkey notification.</param>
    /// <param name="modifiers">Win32 modifier flags.</param>
    /// <param name="virtualKey">Virtual-key code.</param>
    public void Register(Window window, int modifiers, int virtualKey)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        RegisterHotKey(_hwnd, Id, (uint)modifiers, (uint)virtualKey);
    }

    /// <summary>Resumes firing <see cref="Triggered"/> after a prior <see cref="Suspend"/> call.</summary>
    public void Resume() => _suspended = false;

    /// <summary>Temporarily suppresses <see cref="Triggered"/> without unregistering the hotkey.</summary>
    public void Suspend() => _suspended = true;

    /// <summary>
    /// Re-registers the hotkey with new modifier and key values without re-attaching the message hook.
    /// </summary>
    /// <param name="modifiers">New Win32 modifier flags.</param>
    /// <param name="virtualKey">New virtual-key code.</param>
    public void Update(int modifiers, int virtualKey)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        UnregisterHotKey(_hwnd, Id);
        RegisterHotKey(_hwnd, Id, (uint)modifiers, (uint)virtualKey);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
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