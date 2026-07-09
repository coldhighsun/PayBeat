using System.Runtime.InteropServices;
using PayBeat.WinUI.Helpers;

namespace PayBeat.WinUI.Services;

/// <summary>
/// Registers and manages a Win32 global hotkey for toggling widget visibility.
/// Supports suspending the hotkey while the settings window captures key input.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int Id = 0x3001;
    private const uint WmHotkey = 0x0312;
    private nint _hwnd;

    private WindowSubclassHelper? _subclass;

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
        parts.Add(((Windows.System.VirtualKey)virtualKey).ToString());
        return string.Join("+", parts);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_hwnd != 0)
        {
            UnregisterHotKey(_hwnd, Id);
            _subclass?.Dispose();
            _hwnd = 0;
        }
    }

    /// <summary>
    /// Subclasses <paramref name="hwnd"/>'s WndProc and registers the hotkey.
    /// Must be called after the window's HWND is created.
    /// </summary>
    /// <param name="hwnd">The window whose message loop will receive the hotkey notification.</param>
    /// <param name="modifiers">Win32 modifier flags.</param>
    /// <param name="virtualKey">Virtual-key code.</param>
    public bool Register(nint hwnd, int modifiers, int virtualKey)
    {
        _hwnd = hwnd;
        _subclass = new(hwnd);
        _subclass.MessageReceived += OnMessage;
        return RegisterHotKey(_hwnd, Id, (uint)modifiers, (uint)virtualKey);
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
    public bool Update(int modifiers, int virtualKey)
    {
        if (_hwnd == 0)
        {
            return false;
        }
        UnregisterHotKey(_hwnd, Id);
        return RegisterHotKey(_hwnd, Id, (uint)modifiers, (uint)virtualKey);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private nint? OnMessage(uint msg, nuint wParam, nint lParam)
    {
        if (msg == WmHotkey && (int)wParam == Id)
        {
            if (!_suspended)
            {
                Triggered?.Invoke();
            }
            return 0;
        }
        return null;
    }
}