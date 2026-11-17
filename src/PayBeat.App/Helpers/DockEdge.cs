namespace PayBeat.App.Helpers;

/// <summary>
/// Screen edge a window can be docked to for auto-hide.
/// </summary>
public enum DockEdge
{
    /// <summary>
    /// Not docked to any edge.
    /// </summary>
    None,

    /// <summary>
    /// Docked to the left edge.
    /// </summary>
    Left,

    /// <summary>
    /// Docked to the right edge.
    /// </summary>
    Right,

    /// <summary>
    /// Docked to the top edge.
    /// </summary>
    Top,

    /// <summary>
    /// Docked to the bottom edge.
    /// </summary>
    Bottom,
}
