using Avalonia;
using Avalonia.Styling;

namespace PayBeat.App.Services;

/// <summary>
/// Applies the light/dark color theme by switching the application's requested theme variant.
/// Supported themes: <c>"light"</c>, <c>"dark"</c>, and <c>"auto"</c> (follows the operating system).
/// </summary>
public static class ThemeService
{
    /// <summary>
    /// Maps a theme code to the matching Avalonia theme variant; unknown codes follow the system.
    /// </summary>
    /// <param name="theme">Theme code or <c>"auto"</c>.</param>
    /// <returns>The theme variant to request.</returns>
    public static ThemeVariant ToVariant(string theme) => theme switch
    {
        "light" => ThemeVariant.Light,
        "dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    /// <summary>
    /// Requests the theme variant matching <paramref name="theme"/>.
    /// Safe to call multiple times (e.g. after saving settings).
    /// </summary>
    /// <param name="theme">Theme code or <c>"auto"</c>.</param>
    public static void Apply(string theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = ToVariant(theme);
        }
    }
}
