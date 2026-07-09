using Microsoft.UI.Xaml.Data;

namespace PayBeat.WinUI.Converters;

/// <summary>
/// Converts between the numeric-text properties on <see cref="ViewModels.SettingsViewModel"/>
/// (validated as free-form strings) and the <see langword="double"/> <c>Value</c> a <c>Slider</c> binds to.
/// </summary>
public sealed class StringToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string s && double.TryParse(s, out var d) ? d : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is double d ? d.ToString("G29") : "0";
}
