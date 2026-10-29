using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace PayBeat.App.Helpers;

/// <summary>
/// Multi-monitor helpers built on Avalonia's <see cref="Screens"/>. Handles position restore
/// (match by saved screen name, fall back to the nearest monitor) and window pixel metrics.
/// </summary>
internal static class ScreenHelper
{
    /// <summary>
    /// Finds the screen to restore a saved position on. Matches by display name first; falls back
    /// to the screen containing (or nearest to) <paramref name="savedPoint"/>, then the primary screen.
    /// </summary>
    /// <param name="window">Any window, used to reach the screen list.</param>
    /// <param name="savedPoint">The saved top-left position in pixels.</param>
    /// <param name="displayName">The saved screen name, or <see langword="null"/>.</param>
    /// <returns>The chosen screen, or <see langword="null"/> when no screens are available.</returns>
    public static Screen? FindScreenForRestore(Window window, PixelPoint savedPoint, string? displayName)
    {
        var screens = window.Screens;
        var all = screens.All;
        if (all.Count == 0)
        {
            return null;
        }

        if (displayName is not null)
        {
            var byName = all.FirstOrDefault(s => string.Equals(s.DisplayName, displayName, StringComparison.Ordinal));
            if (byName is not null)
            {
                return byName;
            }
        }

        var nearest = WindowPlacement.NearestIndex(all.Select(s => s.Bounds).ToList(), savedPoint);
        return nearest >= 0 ? all[nearest] : screens.Primary;
    }

    /// <summary>
    /// Returns the screen the window is currently on, falling back to the primary screen.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The current screen, or <see langword="null"/> when no screens are available.</returns>
    public static Screen? GetCurrentScreen(Window window) =>
        window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;

    /// <summary>
    /// Returns the name identifying the screen the window is on, used to restore the monitor later.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The screen display name, or <see langword="null"/> when unknown.</returns>
    public static string? GetCurrentScreenName(Window window) =>
        string.IsNullOrEmpty(GetCurrentScreen(window)?.DisplayName) ? null : GetCurrentScreen(window)!.DisplayName;

    /// <summary>
    /// Returns the window's size in device pixels.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The pixel size, or zero when the window has not been measured.</returns>
    public static PixelSize GetPixelSize(Window window)
    {
        var frame = window.FrameSize ?? window.ClientSize;
        var scale = window.RenderScaling;
        return new PixelSize((int)Math.Ceiling(frame.Width * scale), (int)Math.Ceiling(frame.Height * scale));
    }
}
