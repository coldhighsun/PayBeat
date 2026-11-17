using Avalonia;

namespace PayBeat.App.Helpers;

/// <summary>
/// Pure geometry helpers for positioning windows, working in device pixels. Kept free of any
/// windowing types so the logic can be unit tested.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Clamps <paramref name="position"/> so a window of <paramref name="size"/> stays entirely inside
    /// <paramref name="area"/>. When the window is larger than the area it is pinned to the area's origin.
    /// </summary>
    /// <param name="position">Desired top-left corner of the window.</param>
    /// <param name="size">Window size in pixels.</param>
    /// <param name="area">Area the window must stay inside.</param>
    /// <returns>The clamped top-left corner.</returns>
    public static PixelPoint Clamp(PixelPoint position, PixelSize size, PixelRect area)
    {
        var maxX = Math.Max(area.X, area.Right - size.Width);
        var maxY = Math.Max(area.Y, area.Bottom - size.Height);
        return new PixelPoint(
            Math.Clamp(position.X, area.X, maxX),
            Math.Clamp(position.Y, area.Y, maxY));
    }

    /// <summary>
    /// Returns the top-left corner of a window being dragged so that the pointer stays at the same
    /// relative spot of the window, even if the window's pixel size changed since the drag started.
    /// </summary>
    /// <param name="cursor">Pointer position in screen pixels.</param>
    /// <param name="grabFraction">Grab point as a fraction (0-1) of the window's width and height.</param>
    /// <param name="size">Current window size in pixels.</param>
    /// <returns>The window's top-left corner.</returns>
    public static PixelPoint DragPosition(PixelPoint cursor, Point grabFraction, PixelSize size) =>
        new(
            cursor.X - (int)Math.Round(grabFraction.X * size.Width),
            cursor.Y - (int)Math.Round(grabFraction.Y * size.Height));

    /// <summary>
    /// Returns the top-left corner that centers a window of <paramref name="size"/> inside <paramref name="area"/>.
    /// </summary>
    /// <param name="size">Window size in pixels.</param>
    /// <param name="area">Area to center in.</param>
    /// <returns>The centered top-left corner.</returns>
    public static PixelPoint Center(PixelSize size, PixelRect area) =>
        new(area.X + ((area.Width - size.Width) / 2), area.Y + ((area.Height - size.Height) / 2));

    /// <summary>
    /// Returns the top-left corner that docks a window of <paramref name="size"/> to the bottom-right
    /// of <paramref name="area"/>, leaving <paramref name="margin"/> pixels to the edges.
    /// </summary>
    /// <param name="size">Window size in pixels.</param>
    /// <param name="area">Area to dock in (usually a work area).</param>
    /// <param name="margin">Gap to the area's right and bottom edges, in pixels.</param>
    /// <returns>The docked top-left corner.</returns>
    public static PixelPoint BottomRight(PixelSize size, PixelRect area, int margin) =>
        new(area.Right - size.Width - margin, area.Bottom - size.Height - margin);

    /// <summary>
    /// Detects which edge of <paramref name="area"/> a window is docked to. Returns the edge the window is
    /// closest to, or has been pushed past, when that gap is within <paramref name="threshold"/> pixels, or
    /// <see cref="DockEdge.None"/>. A window dragged against the screen edge by its body sticks out beyond the edge
    /// (the pointer cannot leave the screen), so overshoot counts as docked as long as the window still overlaps the area.
    /// </summary>
    /// <param name="position">Top-left corner of the window.</param>
    /// <param name="size">Window size in pixels.</param>
    /// <param name="area">Screen bounds the window may dock to.</param>
    /// <param name="threshold">Maximum gap, in pixels, that still counts as docked.</param>
    /// <returns>The docked edge, or <see cref="DockEdge.None"/>.</returns>
    public static DockEdge DetectDockEdge(PixelPoint position, PixelSize size, PixelRect area, int threshold)
    {
        if (!new PixelRect(position, size).Intersects(area))
        {
            return DockEdge.None;
        }

        var best = DockEdge.None;
        var bestGap = int.MaxValue;

        // Signed gaps: negative when the window sticks out beyond the edge.
        void Consider(DockEdge edge, int gap)
        {
            if (gap <= threshold && gap < bestGap)
            {
                best = edge;
                bestGap = gap;
            }
        }

        Consider(DockEdge.Left, position.X - area.X);
        Consider(DockEdge.Right, area.Right - (position.X + size.Width));
        Consider(DockEdge.Top, position.Y - area.Y);
        Consider(DockEdge.Bottom, area.Bottom - (position.Y + size.Height));
        return best;
    }

    /// <summary>
    /// Returns the position that places the window flush against <paramref name="edge"/> of
    /// <paramref name="area"/>, keeping it fully inside the area on the other axis.
    /// </summary>
    /// <param name="edge">The edge to dock to.</param>
    /// <param name="position">Current top-left corner of the window.</param>
    /// <param name="size">Window size in pixels.</param>
    /// <param name="area">Screen bounds.</param>
    /// <returns>The docked top-left corner.</returns>
    public static PixelPoint DockedPosition(DockEdge edge, PixelPoint position, PixelSize size, PixelRect area)
    {
        var clamped = Clamp(position, size, area);
        return edge switch
        {
            DockEdge.Left => new PixelPoint(area.X, clamped.Y),
            DockEdge.Right => new PixelPoint(area.Right - size.Width, clamped.Y),
            DockEdge.Top => new PixelPoint(clamped.X, area.Y),
            DockEdge.Bottom => new PixelPoint(clamped.X, area.Bottom - size.Height),
            _ => clamped,
        };
    }

    /// <summary>
    /// Returns the position that slides a docked window off-screen across <paramref name="edge"/>,
    /// leaving only a <paramref name="strip"/>-pixel sliver visible.
    /// </summary>
    /// <param name="edge">The edge the window is docked to.</param>
    /// <param name="docked">The docked (fully visible) top-left corner.</param>
    /// <param name="size">Window size in pixels.</param>
    /// <param name="area">Screen bounds.</param>
    /// <param name="strip">Visible sliver thickness in pixels.</param>
    /// <returns>The hidden top-left corner.</returns>
    public static PixelPoint HiddenPosition(DockEdge edge, PixelPoint docked, PixelSize size, PixelRect area, int strip) =>
        edge switch
        {
            DockEdge.Left => new PixelPoint(area.X + strip - size.Width, docked.Y),
            DockEdge.Right => new PixelPoint(area.Right - strip, docked.Y),
            DockEdge.Top => new PixelPoint(docked.X, area.Y + strip - size.Height),
            DockEdge.Bottom => new PixelPoint(docked.X, area.Bottom - strip),
            _ => docked,
        };

    /// <summary>
    /// Returns the squared distance from <paramref name="point"/> to the closest point of
    /// <paramref name="rect"/> (zero when the point lies inside).
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="point">The point.</param>
    /// <returns>The squared distance in pixels.</returns>
    public static long DistanceSquared(PixelRect rect, PixelPoint point)
    {
        long dx = Math.Max(Math.Max(rect.X - point.X, 0), point.X - (rect.Right - 1));
        long dy = Math.Max(Math.Max(rect.Y - point.Y, 0), point.Y - (rect.Bottom - 1));
        return (dx * dx) + (dy * dy);
    }

    /// <summary>
    /// Returns the index of the rectangle nearest to <paramref name="point"/>, or <c>-1</c> when
    /// <paramref name="rects"/> is empty. Ties resolve to the earliest rectangle.
    /// </summary>
    /// <param name="rects">Candidate rectangles (e.g. monitor bounds).</param>
    /// <param name="point">The reference point.</param>
    /// <returns>The index of the nearest rectangle, or <c>-1</c>.</returns>
    public static int NearestIndex(IReadOnlyList<PixelRect> rects, PixelPoint point)
    {
        var best = -1;
        var bestDistance = long.MaxValue;
        for (var i = 0; i < rects.Count; i++)
        {
            var distance = DistanceSquared(rects[i], point);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }
}
