using Avalonia.Controls;

namespace PayBeat.App.Platform;

/// <summary>
/// Re-asserts a window's topmost z-order on platforms where other applications can steal it
/// even though <see cref="Window.Topmost"/> is still set.
/// </summary>
public interface ITopmostEnforcer : IDisposable
{
    /// <summary>
    /// Starts watching for z-order changes and invokes <paramref name="onForegroundChanged"/>
    /// on the UI thread whenever another window may have taken the topmost band.
    /// </summary>
    /// <param name="onForegroundChanged">Callback invoked on foreground changes.</param>
    void Watch(Action onForegroundChanged);

    /// <summary>
    /// Re-applies the topmost band to <paramref name="window"/> without moving, resizing or activating it.
    /// </summary>
    /// <param name="window">The window to bring back to the top of the topmost band.</param>
    void ForceTopmost(Window window);
}
