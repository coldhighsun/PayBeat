using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using PayBeat.App.Views;

namespace PayBeat.App.Tests.Views;

/// <summary>
/// Tests for <see cref="ToastWindow"/> sizing.
/// </summary>
public class ToastWindowTests
{
    /// <summary>
    /// Verifies a toast sizes its width to its content and stays within its own min/max width limits.
    /// </summary>
    [AvaloniaFact]
    public void Show_ShortVersusLongText_WidthFollowsContentAndStaysWithinLimits()
    {
        var shortToast = new ToastWindow("Hi", "Ok", TimeSpan.FromSeconds(30));
        var longToast = new ToastWindow(
            "A rather long notification title that keeps going",
            new string('x', 40) + " " + string.Join(' ', Enumerable.Repeat("long body text", 20)),
            TimeSpan.FromSeconds(30));

        try
        {
            shortToast.Show();
            longToast.Show();
            shortToast.UpdateLayout();
            longToast.UpdateLayout();

            var shortWidth = shortToast.Bounds.Width;
            var longWidth = longToast.Bounds.Width;

            shortWidth.Should().BeLessThan(longWidth);
            shortWidth.Should().BeGreaterThanOrEqualTo(shortToast.MinWidth);
            longWidth.Should().BeLessThanOrEqualTo(longToast.MaxWidth);
        }
        finally
        {
            shortToast.Close();
            longToast.Close();
        }
    }

    /// <summary>
    /// Verifies the declared width limits are sane: a positive minimum below the maximum.
    /// </summary>
    [AvaloniaFact]
    public void Constructor_Default_DeclaresMinWidthBelowMaxWidth()
    {
        var toast = new ToastWindow();

        try
        {
            toast.MinWidth.Should().BeGreaterThan(0);
            toast.MinWidth.Should().BeLessThan(toast.MaxWidth);
        }
        finally
        {
            toast.Close();
        }
    }
}
