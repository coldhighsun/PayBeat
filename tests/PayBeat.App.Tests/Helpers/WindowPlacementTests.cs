using Avalonia;
using AwesomeAssertions;
using PayBeat.App.Helpers;

namespace PayBeat.App.Tests.Helpers;

/// <summary>
/// Tests for <see cref="WindowPlacement"/>.
/// </summary>
public class WindowPlacementTests
{
    /// <summary>
    /// A 1920x1080 screen at the origin.
    /// </summary>
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

    /// <summary>
    /// Verifies the pointer keeps its relative spot in the window when the pixel size differs from the grab time.
    /// </summary>
    [Fact]
    public void DragPosition_WindowResizedByScaling_KeepsRelativeGrabPoint()
    {
        var grab = new Point(0.25, 0.5);

        var atFullScale = WindowPlacement.DragPosition(new PixelPoint(1000, 500), grab, new PixelSize(200, 100));
        var atHalfScale = WindowPlacement.DragPosition(new PixelPoint(1000, 500), grab, new PixelSize(100, 50));

        atFullScale.Should().Be(new PixelPoint(950, 450));
        atHalfScale.Should().Be(new PixelPoint(975, 475));
    }

    /// <summary>
    /// Verifies a window that already fits is left where it is.
    /// </summary>
    [Fact]
    public void Clamp_WindowInsideArea_ReturnsSamePosition()
    {
        var result = WindowPlacement.Clamp(new PixelPoint(100, 200), new PixelSize(300, 150), Screen);

        result.Should().Be(new PixelPoint(100, 200));
    }

    /// <summary>
    /// Verifies a window hanging off the right and bottom edges is pulled back in.
    /// </summary>
    [Fact]
    public void Clamp_WindowPastBottomRight_PullsBackInside()
    {
        var result = WindowPlacement.Clamp(new PixelPoint(1900, 1070), new PixelSize(300, 150), Screen);

        result.Should().Be(new PixelPoint(1620, 930));
    }

    /// <summary>
    /// Verifies a window left of or above the area snaps to the area's origin.
    /// </summary>
    [Fact]
    public void Clamp_WindowBeforeTopLeft_SnapsToOrigin()
    {
        var area = new PixelRect(1920, -48, 1920, 1200);

        var result = WindowPlacement.Clamp(new PixelPoint(-50, -200), new PixelSize(300, 150), area);

        result.Should().Be(new PixelPoint(1920, -48));
    }

    /// <summary>
    /// Verifies a window larger than the area is pinned to the origin instead of throwing.
    /// </summary>
    [Fact]
    public void Clamp_WindowLargerThanArea_PinsToOrigin()
    {
        var result = WindowPlacement.Clamp(new PixelPoint(500, 500), new PixelSize(3000, 2000), Screen);

        result.Should().Be(new PixelPoint(0, 0));
    }

    /// <summary>
    /// Verifies centering inside an offset area.
    /// </summary>
    [Fact]
    public void Center_OffsetArea_CentersWindow()
    {
        var area = new PixelRect(1920, 0, 1920, 1080);

        var result = WindowPlacement.Center(new PixelSize(400, 200), area);

        result.Should().Be(new PixelPoint(2680, 440));
    }

    /// <summary>
    /// Verifies docking to the bottom-right corner with a margin.
    /// </summary>
    [Fact]
    public void BottomRight_WithMargin_DocksToCorner()
    {
        var result = WindowPlacement.BottomRight(new PixelSize(300, 150), Screen, 8);

        result.Should().Be(new PixelPoint(1612, 922));
    }

    /// <summary>
    /// Verifies the distance to a rectangle is zero for points inside it.
    /// </summary>
    [Fact]
    public void DistanceSquared_PointInside_ReturnsZero()
    {
        WindowPlacement.DistanceSquared(Screen, new PixelPoint(10, 10)).Should().Be(0);
    }

    /// <summary>
    /// Verifies the nearest rectangle is chosen for a point between two monitors.
    /// </summary>
    [Fact]
    public void NearestIndex_PointNearSecondScreen_ReturnsSecondIndex()
    {
        var screens = new[] { Screen, new PixelRect(1920, 0, 1920, 1080) };

        var index = WindowPlacement.NearestIndex(screens, new PixelPoint(5000, 500));

        index.Should().Be(1);
    }

    /// <summary>
    /// Verifies an empty list yields no match.
    /// </summary>
    [Fact]
    public void NearestIndex_NoScreens_ReturnsMinusOne()
    {
        WindowPlacement.NearestIndex([], new PixelPoint(0, 0)).Should().Be(-1);
    }
}
