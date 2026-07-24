using Microsoft.Win32;

namespace PayBeat.App.Services;

/// <summary>
/// Swaps the active color-palette resource dictionary at runtime to implement light/dark theming.
/// Supported themes: <c>"light"</c>, <c>"dark"</c>, and <c>"auto"</c> (resolves from the Windows apps theme setting).
/// </summary>
public static class ThemeService
{
    private const string DarkUri = "pack://application:,,,/Resources/Theme.Dark.xaml";
    private const string LightUri = "pack://application:,,,/Resources/Theme.Light.xaml";

    /// <summary>
    /// Replaces the currently loaded palette dictionary with the one matching <paramref name="theme"/>.
    /// Safe to call multiple times (e.g. after saving settings).
    /// </summary>
    /// <param name="theme">Theme code or <c>"auto"</c>.</param>
    public static void Apply(string theme)
    {
        var resolved = Resolve(theme);
        var dicts = Application.Current.Resources.MergedDictionaries;

        var old = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString is DarkUri or LightUri);

        if (old != null)
        {
            dicts.Remove(old);
        }

        var uri = new Uri(resolved == "light" ? LightUri : DarkUri);
        dicts.Add(new ResourceDictionary { Source = uri });
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static string Resolve(string theme)
    {
        if (theme != "auto")
        {
            return theme;
        }

        return IsSystemLightTheme() ? "light" : "dark";
    }
}