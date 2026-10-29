using Avalonia.Controls;

namespace PayBeat.App.Platform;

/// <summary>
/// No-op <see cref="ITopmostEnforcer"/>; relies on <see cref="Window.Topmost"/> alone.
/// </summary>
public sealed class NullTopmostEnforcer : ITopmostEnforcer
{
    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public void ForceTopmost(Window window)
    {
    }

    /// <inheritdoc/>
    public void Watch(Action onForegroundChanged)
    {
    }
}
