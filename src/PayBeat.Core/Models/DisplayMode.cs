namespace PayBeat.Core.Models;

/// <summary>
/// Represents the widget display modes, each with its own view template and saved window position.
/// </summary>
public enum DisplayMode
{
    /// <summary>No window is shown; only the tray icon remains.</summary>
    None = 0,

    /// <summary>Full-size view showing earnings, progress bar, and rate.</summary>
    Normal,

    /// <summary>Minimal view showing only the earnings amount.</summary>
    Mini,

    /// <summary>
    /// Fullscreen "show-off" view occupying the entire screen with a huge earnings figure,
    /// full stats, and decorative animation. Draggable; fills the current monitor and can be moved
    /// to another monitor.
    /// </summary>
    Flex,
}