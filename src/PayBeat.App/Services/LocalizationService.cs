using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PayBeat.App.Services;

/// <summary>
/// Swaps the active string resources at runtime to implement UI localization.
/// Supported languages: <c>"en"</c>, <c>"zh-CN"</c>, and <c>"auto"</c> (resolves from OS UI culture).
/// </summary>
public static class LocalizationService
{
    /// <summary>
    /// Resource location of the English string dictionary.
    /// </summary>
    private const string EnUri = "avares://PayBeat/Resources/Strings.en.axaml";

    /// <summary>
    /// Resource location of the Simplified Chinese string dictionary.
    /// </summary>
    private const string ZhUri = "avares://PayBeat/Resources/Strings.zh-CN.axaml";

    /// <summary>
    /// Copies the strings matching <paramref name="language"/> into the application resources,
    /// overwriting the previous values so <c>DynamicResource</c> bindings refresh.
    /// Safe to call multiple times (e.g. after saving settings).
    /// </summary>
    /// <param name="language">Language code or <c>"auto"</c>.</param>
    public static void Apply(string language)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var uri = new Uri(Resolve(language) == "zh-CN" ? ZhUri : EnUri);
        var dictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
        foreach (var (key, value) in dictionary)
        {
            app.Resources[key] = value;
        }
    }

    /// <summary>
    /// Looks up a localized string by <paramref name="key"/>. Returns <paramref name="key"/> itself
    /// as a fallback when the resource is not found or no application is running.
    /// </summary>
    /// <param name="key">Resource key defined in the active Strings dictionary.</param>
    /// <returns>The localized string, or <paramref name="key"/>.</returns>
    public static string Get(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is string text
            ? text
            : key;

    /// <summary>
    /// Resolves <c>"auto"</c> to a concrete language code using the OS UI culture.
    /// </summary>
    /// <param name="language">Language code or <c>"auto"</c>.</param>
    /// <returns>A concrete language code.</returns>
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
