using Avalonia.Controls;

namespace PayBeat.App.Platform;

/// <summary>
/// Registers a system-wide hotkey that toggles widget visibility. Implementations are platform
/// specific; unsupported platforms use <see cref="NullHotkeyService"/>.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Raised on the UI thread when the hotkey is pressed and not suspended.
    /// </summary>
    event Action? Triggered;

    /// <summary>
    /// Gets a value indicating whether global hotkeys work on the current platform. When
    /// <see langword="false"/>, the hotkey section of the settings window is hidden.
    /// </summary>
    bool IsSupported
    {
        get;
    }

    /// <summary>
    /// Registers the hotkey against <paramref name="window"/>.
    /// </summary>
    /// <param name="window">The window whose native handle receives the hotkey notification.</param>
    /// <param name="modifiers">Win32-style modifier flags (Alt=1, Ctrl=2, Shift=4, Win=8).</param>
    /// <param name="virtualKey">Win32-style virtual-key code.</param>
    /// <returns><see langword="true"/> when the hotkey was registered.</returns>
    bool Register(Window window, int modifiers, int virtualKey);

    /// <summary>
    /// Re-registers the hotkey with new values.
    /// </summary>
    /// <param name="modifiers">New modifier flags.</param>
    /// <param name="virtualKey">New virtual-key code.</param>
    /// <returns><see langword="true"/> when the hotkey was registered.</returns>
    bool Update(int modifiers, int virtualKey);

    /// <summary>
    /// Temporarily suppresses <see cref="Triggered"/> without unregistering the hotkey.
    /// </summary>
    void Suspend();

    /// <summary>
    /// Resumes firing <see cref="Triggered"/> after a prior <see cref="Suspend"/> call.
    /// </summary>
    void Resume();
}
