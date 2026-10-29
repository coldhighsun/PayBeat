using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PayBeat.App.Views;

/// <summary>
/// Fullscreen "show-off" widget view: a huge earnings figure with a decorative animated
/// background, glow pulse, and full workday stats. Used only by <see cref="Models.DisplayMode.Flex"/>.
/// The looping animations are declared as styles in XAML; this code-behind only applies the theme colors.
/// </summary>
public partial class FlexView : UserControl
{
    /// <summary>
    /// Initializes a new instance of <see cref="FlexView"/> and loads the XAML component tree.
    /// </summary>
    public FlexView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => ApplyThemeColors();
        ActualThemeVariantChanged += (_, _) => ApplyThemeColors();
    }

    /// <summary>
    /// Looks up a color from the theme palette for the view's current theme variant.
    /// </summary>
    /// <param name="key">Resource key of the color.</param>
    /// <param name="fallback">Color returned when the resource cannot be found.</param>
    private Color ThemeColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color ? color : fallback;

    /// <summary>
    /// Applies the theme colors to the gradients and the amount glow.
    /// </summary>
    private void ApplyThemeColors()
    {
        var baseColor = ThemeColor("BaseColor", Colors.Black);
        var surfaceColor = ThemeColor("SurfaceColor", Colors.DimGray);
        var midColor = ThemeColor("FlexGradientMidColor", baseColor);
        var greenColor = ThemeColor("GreenColor", Colors.LightGreen);

        SetGradient(((LinearGradientBrush)RootGrid.Background!).GradientStops, baseColor, midColor);
        SetGradient(((LinearGradientBrush)PulseOverlay.Background!).GradientStops, baseColor, surfaceColor);
        AmountText.Effect = new DropShadowEffect
        {
            Color = greenColor,
            Opacity = 0.55,
            BlurRadius = 40,
            OffsetX = 0,
            OffsetY = 0,
        };
    }

    /// <summary>
    /// Fills a three-stop gradient with the base color at both ends and <paramref name="middle"/> in the center.
    /// </summary>
    private static void SetGradient(GradientStops stops, Color edge, Color middle)
    {
        stops[0].Color = edge;
        stops[1].Color = middle;
        stops[2].Color = edge;
    }
}
