using System.Globalization;

namespace PayBeat.App.Services;

/// <summary>
/// Swaps the active string resource dictionary at runtime to implement UI localization.
/// Supported languages: <c>"en"</c>, <c>"zh-CN"</c>, and <c>"auto"</c> (resolves from OS UI culture).
/// </summary>
public static class LocalizationService
{
    private const string EnUri = "pack://application:,,,/Resources/Strings.en.xaml";
    private const string ZhUri = "pack://application:,,,/Resources/Strings.zh-CN.xaml";

    /// <summary>
    /// Replaces the currently loaded string dictionary with the one matching <paramref name="language"/>.
    /// Safe to call multiple times (e.g. after saving settings).
    /// </summary>
    /// <param name="language">Language code or <c>"auto"</c>.</param>
    public static void Apply(string language)
    {
        var resolved = Resolve(language);
        var dicts = Application.Current.Resources.MergedDictionaries;

        var old = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString is EnUri or ZhUri);

        if (old != null)
        {
            dicts.Remove(old);
        }

        var uri = new Uri(resolved == "zh-CN" ? ZhUri : EnUri);
        dicts.Add(new ResourceDictionary { Source = uri });
    }

    /// <summary>
    /// Looks up a localized string by <paramref name="key"/>. Returns <paramref name="key"/> itself
    /// as a fallback when the resource is not found.
    /// </summary>
    /// <param name="key">Resource key defined in the active Strings dictionary.</param>
    public static string Get(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    private static string Resolve(string language)
    {
        if (language != "auto")
        {
            return language;
        }

        var name = CultureInfo.CurrentUICulture.Name;
        return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
    }
}