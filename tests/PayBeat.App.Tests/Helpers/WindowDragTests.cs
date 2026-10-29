using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AwesomeAssertions;
using PayBeat.App.Helpers;

namespace PayBeat.App.Tests.Helpers;

/// <summary>
/// Tests for <see cref="WindowDrag"/>.
/// </summary>
public class WindowDragTests
{
    /// <summary>
    /// Verifies a press on a combo box (or content inside a control) must not start a window drag.
    /// </summary>
    [AvaloniaFact]
    public void IsInteractive_ComboBoxAndChild_ReturnsTrue()
    {
        var inner = new Border();
        var combo = new ComboBox();
        var button = new Button { Content = inner };
        var window = new Window { Content = new StackPanel { Children = { combo, button } } };
        window.Show();

        WindowDrag.IsInteractive(combo).Should().BeTrue();
        WindowDrag.IsInteractive(inner).Should().BeTrue();
        WindowDrag.IsInteractive(button).Should().BeTrue();
        window.Close();
    }

    /// <summary>
    /// Verifies a press on plain background still drags the window.
    /// </summary>
    [AvaloniaFact]
    public void IsInteractive_PlainPanel_ReturnsFalse()
    {
        var label = new TextBlock { Text = "x" };
        var window = new Window { Content = new StackPanel { Children = { label } } };
        window.Show();

        WindowDrag.IsInteractive(label).Should().BeFalse();
        WindowDrag.IsInteractive(null).Should().BeFalse();
        window.Close();
    }

    /// <summary>
    /// Verifies a press-move-release on plain content moves the window and reports that it moved.
    /// </summary>
    [AvaloniaFact]
    public void TryBegin_PressMoveRelease_MovesWindowAndReportsMoved()
    {
        var window = CreateDraggableWindow(out var results);
        var start = window.Position;

        window.MouseDown(new Point(50, 50), MouseButton.Left);
        window.MouseMove(new Point(80, 70));
        window.MouseUp(new Point(80, 70), MouseButton.Left);

        window.Position.Should().NotBe(start);
        results.Should().Equal(true);
        window.Close();
    }

    /// <summary>
    /// Verifies a plain click that never moves the pointer reports that the window did not move.
    /// </summary>
    [AvaloniaFact]
    public void TryBegin_ClickWithoutMove_ReportsNotMoved()
    {
        var window = CreateDraggableWindow(out var results);
        var start = window.Position;

        window.MouseDown(new Point(50, 50), MouseButton.Left);
        window.MouseUp(new Point(50, 50), MouseButton.Left);

        window.Position.Should().Be(start);
        results.Should().Equal(false);
        window.Close();
    }

    /// <summary>
    /// Verifies moves after the button was released no longer drag the window and the end is reported once.
    /// </summary>
    [AvaloniaFact]
    public void TryBegin_MoveAfterRelease_DoesNotMoveWindow()
    {
        var window = CreateDraggableWindow(out var results);

        window.MouseDown(new Point(50, 50), MouseButton.Left);
        window.MouseUp(new Point(50, 50), MouseButton.Left);
        var afterRelease = window.Position;
        window.MouseMove(new Point(120, 90));

        window.Position.Should().Be(afterRelease);
        results.Should().HaveCount(1);
        window.Close();
    }

    /// <summary>
    /// Verifies a press on an interactive control does not start a drag.
    /// </summary>
    [AvaloniaFact]
    public void TryBegin_PressOnButton_DoesNotStartDrag()
    {
        var results = new List<bool>();
        var window = new Window { Width = 200, Height = 100, Content = new Button { Content = "x", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch } };
        window.PointerPressed += (_, e) => WindowDrag.TryBegin(window, e, results.Add);
        window.Show();

        window.MouseDown(new Point(100, 50), MouseButton.Left);
        window.MouseMove(new Point(130, 70));
        window.MouseUp(new Point(130, 70), MouseButton.Left);

        results.Should().BeEmpty();
        window.Close();
    }

    /// <summary>
    /// Creates a shown window on plain content that starts a drag from every left press.
    /// </summary>
    /// <param name="results">Receives the <c>moved</c> flag of every finished drag.</param>
    private static Window CreateDraggableWindow(out List<bool> results)
    {
        var ended = new List<bool>();
        var window = new Window { Width = 200, Height = 100, Position = new PixelPoint(100, 100), Content = new Border { Background = Brushes.Gray } };
        window.PointerPressed += (_, e) => WindowDrag.TryBegin(window, e, ended.Add);
        window.Show();
        results = ended;
        return window;
    }

    /// <summary>
    /// Verifies buttons, sliders and text boxes are also treated as interactive.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(typeof(Button))]
    [InlineData(typeof(Slider))]
    [InlineData(typeof(TextBox))]
    [InlineData(typeof(CheckBox))]
    [InlineData(typeof(TabItem))]
    public void IsInteractive_InputControl_ReturnsTrue(Type controlType)
    {
        var control = (Control)Activator.CreateInstance(controlType)!;

        WindowDrag.IsInteractive(control).Should().BeTrue();
    }
}
