using Avalonia;

namespace PayBeat.App.Services;

/// <summary>
/// Pure layout math for stacked toast notifications, in device pixels. Toasts dock to the
/// bottom-right of the work area; the oldest toast sits at the bottom and newer ones stack upwards.
/// </summary>
public static class ToastLayout
{
    /// <summary>
    /// Computes the top-left corner of every toast.
    /// </summary>
    /// <param name="workArea">Work area of the screen the toasts appear on.</param>
    /// <param name="sizes">Toast sizes, oldest first.</param>
    /// <param name="margin">Gap between the stack and the work area edges, in pixels.</param>
    /// <param name="gap">Gap between two stacked toasts, in pixels.</param>
    /// <returns>One top-left position per toast, in the same order as <paramref name="sizes"/>.</returns>
    public static IReadOnlyList<PixelPoint> Stack(PixelRect workArea, IReadOnlyList<PixelSize> sizes, int margin, int gap)
    {
        var positions = new List<PixelPoint>(sizes.Count);
        var bottom = workArea.Bottom - margin;
        foreach (var size in sizes)
        {
            var top = bottom - size.Height;
            positions.Add(new PixelPoint(workArea.Right - margin - size.Width, top));
            bottom = top - gap;
        }

        return positions;
    }
}
