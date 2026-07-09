using System.ComponentModel;
using System.Globalization;

namespace PayBeat.WinUI.Services;

/// <summary>
/// Provides runtime-switchable UI localization. WinUI3 has no <c>DynamicResource</c>-style
/// hot-swappable <c>ResourceDictionary</c>, so instead of the WPF build's dictionary-swap
/// approach this exposes a singleton indexer that raises <see cref="INotifyPropertyChanged"/> on
/// <see cref="Apply"/>, letting <c>{x:Bind Instance[Some.Key]}</c> bindings re-evaluate live when
/// the language changes. Supported languages: <c>"en"</c>, <c>"zh-CN"</c>, and <c>"auto"</c>
/// (resolves from OS UI culture).
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    /// <summary>The property-changed name x:Bind/Binding recognize as "any indexed value may have changed".</summary>
    private const string IndexerPropertyName = "Item[]";

    private Dictionary<string, string> _strings = new();

    private LocalizationService()
    {
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Singleton instance, referenced from XAML via <c>{x:Bind LocalizationService.Instance[...]}</c>.</summary>
    public static LocalizationService Instance { get; } = new();

    /// <summary>
    /// Looks up a localized string by <paramref name="key"/>. Returns <paramref name="key"/> itself
    /// as a fallback when the resource is not found.
    /// </summary>
    public string this[string key] => _strings.GetValueOrDefault(key, key);

    /// <summary>
    /// Loads the string table matching <paramref name="language"/> and notifies bindings that
    /// every indexed value may have changed. Safe to call multiple times (e.g. after saving settings).
    /// </summary>
    /// <param name="language">Language code or <c>"auto"</c>.</param>
    public void Apply(string language)
    {
        var resolved = Resolve(language);
        var fileName = resolved == "zh-CN" ? "Strings.zh-CN.json" : "Strings.en.json";
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);

        var json = File.ReadAllText(path);
        _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];

        PropertyChanged?.Invoke(this, new(IndexerPropertyName));
    }

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