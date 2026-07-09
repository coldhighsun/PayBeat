using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PayBeat.App.Views;

/// <summary>
/// Fullscreen "show-off" widget view: a huge earnings figure with a decorative animated
/// background, glow pulse, and full workday stats. Used only by <see cref="PayBeat.Core.Models.DisplayMode.Flex"/>.
/// </summary>
public partial class FlexView
{
    /// <summary>
    /// Initializes a new instance of <see cref="FlexView"/> and loads the XAML component tree.
    /// </summary>
    public FlexView()
    {
        InitializeComponent();
        Loaded += (_, _) => StartAnimations();
        Unloaded += (_, _) => StopAnimations();
    }

    private void StartAnimations()
    {
        var backgroundAnimation = new ColorAnimation
        {
            From = System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x25),
            To = System.Windows.Media.Color.FromRgb(0x31, 0x32, 0x44),
            Duration = TimeSpan.FromSeconds(6),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        BackgroundStopMiddle.BeginAnimation(GradientStop.ColorProperty, backgroundAnimation);

        var glowAnimation = new DoubleAnimation
        {
            From = 0.08,
            To = 0.22,
            Duration = TimeSpan.FromSeconds(2.2),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        GlowEllipse.BeginAnimation(OpacityProperty, glowAnimation);

        var scaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.03,
            Duration = TimeSpan.FromSeconds(1.6),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        AmountScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        AmountScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }

    private void StopAnimations()
    {
        BackgroundStopMiddle.BeginAnimation(GradientStop.ColorProperty, null);
        GlowEllipse.BeginAnimation(OpacityProperty, null);
        AmountScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        AmountScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }
}