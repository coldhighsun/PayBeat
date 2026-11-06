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
    /// Verifies a toast sizes its width to its content and stays within its max width limit.
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
            shortWidth.Should().BeGreaterThan(0);
            longWidth.Should().BeLessThanOrEqualTo(longToast.MaxWidth);
        }
        finally
        {
            shortToast.Close();
            longToast.Close();
        }
    }

    /// <summary>
    /// Verifies the toast declares no minimum width, only a maximum.
    /// </summary>
    [AvaloniaFact]
    public void Constructor_Default_DeclaresNoMinWidth()
    {
        var toast = new ToastWindow();

        try
        {
            toast.MinWidth.Should().Be(0);
            toast.MaxWidth.Should().BeGreaterThan(0);
        }
        finally
        {
            toast.Close();
        }
    }
}
