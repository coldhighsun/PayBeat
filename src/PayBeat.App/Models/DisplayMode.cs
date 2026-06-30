namespace PayBeat.App.Models;

/// <summary>
/// Represents the three widget display modes, each with its own view template and saved window position.
/// </summary>
public enum DisplayMode
{
    /// <summary>Full-size view showing earnings, progress bar, and rate.</summary>
    Normal,

    /// <summary>Condensed single-line view showing earnings and progress.</summary>
    Compact,

    /// <summary>Minimal view showing only the earnings amount.</summary>
    Mini,
}