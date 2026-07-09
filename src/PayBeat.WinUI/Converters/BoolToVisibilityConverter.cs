using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PayBeat.WinUI.Converters;

/// <summary>
/// Converts <see langword="bool"/> to <see cref="Visibility"/> for classic <c>{Binding}</c>
/// expressions. Unlike WPF, WinUI3 has no implicit bool→Visibility coercion.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
