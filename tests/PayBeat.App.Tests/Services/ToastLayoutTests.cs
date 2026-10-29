using Avalonia;
using AwesomeAssertions;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Services;

/// <summary>
/// Tests for <see cref="ToastLayout"/>.
/// </summary>
public class ToastLayoutTests
{
    /// <summary>
    /// A 1920x1040 work area (taskbar excluded).
    /// </summary>
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    /// <summary>
    /// Verifies a single toast docks to the bottom-right corner.
    /// </summary>
    [Fact]
    public void Stack_SingleToast_DocksToBottomRight()
    {
        var positions = ToastLayout.Stack(WorkArea, [new PixelSize(320, 80)], 12, 8);

        positions.Should().Equal(new PixelPoint(1588, 948));
    }

    /// <summary>
    /// Verifies newer toasts stack upwards with the configured gap.
    /// </summary>
    [Fact]
    public void Stack_TwoToasts_StacksUpwardsWithGap()
    {
        var positions = ToastLayout.Stack(WorkArea, [new PixelSize(320, 80), new PixelSize(320, 100)], 12, 8);

        positions.Should().Equal(new PixelPoint(1588, 948), new PixelPoint(1588, 840));
    }

    /// <summary>
    /// Verifies toasts of different widths stay right-aligned.
    /// </summary>
    [Fact]
    public void Stack_DifferentWidths_RightAligns()
    {
        var positions = ToastLayout.Stack(WorkArea, [new PixelSize(200, 50), new PixelSize(400, 50)], 10, 0);

        positions[0].X.Should().Be(1710);
        positions[1].X.Should().Be(1510);
    }

    /// <summary>
    /// Verifies no toasts yield no positions.
    /// </summary>
    [Fact]
    public void Stack_NoToasts_ReturnsEmpty()
    {
        ToastLayout.Stack(WorkArea, [], 12, 8).Should().BeEmpty();
    }
}
