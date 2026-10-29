using Avalonia.Controls;

namespace PayBeat.App.Platform;

/// <summary>
/// No-op <see cref="IHotkeyService"/> used on platforms without global hotkey support.
/// </summary>
public sealed class NullHotkeyService : IHotkeyService
{
    /// <inheritdoc/>
    public event Action? Triggered
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool IsSupported => false;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public bool Register(Window window, int modifiers, int virtualKey) => true;

    /// <inheritdoc/>
    public void Resume()
    {
    }

    /// <inheritdoc/>
    public void Suspend()
    {
    }

    /// <inheritdoc/>
    public bool Update(int modifiers, int virtualKey) => true;
}
