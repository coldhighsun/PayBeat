using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PayBeat.App.Helpers;

/// <summary>
/// Drag-to-move support for borderless windows. Dragging must not start on interactive controls:
/// the pointer would be captured by the window, which for example keeps a <see cref="ComboBox"/>
/// from ever opening. The window is moved by hand instead of with <c>BeginMoveDrag</c>: the native
/// move loop keeps a fixed pixel offset to the pointer, so the grab point drifts when the window is
/// resized by a scaling change while crossing monitors.
/// </summary>
public static class WindowDrag
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="source"/> is, or sits inside, a control that
    /// handles pointer input itself.
    /// </summary>
    /// <param name="source">The element the pointer event originated from.</param>
    /// <returns>Whether the element belongs to an interactive control.</returns>
    public static bool IsInteractive(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ComboBox or ComboBoxItem or TextBox or Button or ToggleButton or Slider or Thumb or ScrollBar or TabItem or MenuItem)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Starts moving <paramref name="window"/> when the left button was pressed on non-interactive content.
    /// </summary>
    /// <param name="window">The window to move.</param>
    /// <param name="e">The pointer-pressed event.</param>
    /// <param name="onEnded">Called once when the drag ends; the argument tells whether the window actually moved.</param>
    public static void TryBegin(Window window, PointerPressedEventArgs e, Action<bool>? onEnded = null)
    {
        if (e.Handled || !e.GetCurrentPoint(window).Properties.IsLeftButtonPressed || IsInteractive(e.Source))
        {
            return;
        }

        if (window.Bounds.Width <= 0 || window.Bounds.Height <= 0)
        {
            return;
        }

        var grab = e.GetPosition(window);
        var fraction = new Point(grab.X / window.Bounds.Width, grab.Y / window.Bounds.Height);
        var session = new DragSession(window, fraction, onEnded);
        e.Pointer.Capture(window);
        session.Attach();
    }

    /// <summary>
    /// One drag gesture: moves the window with the pointer until the button is released or the capture is lost.
    /// </summary>
    /// <param name="window">The window being moved.</param>
    /// <param name="grabFraction">
    /// Where the pointer grabbed the window, as a fraction of its width and height. A fraction (not an offset)
    /// keeps the pointer at the same spot of the window when its pixel size changes between monitors.
    /// </param>
    /// <param name="onEnded">Called once when the drag ends.</param>
    private sealed class DragSession(Window window, Point grabFraction, Action<bool>? onEnded)
    {
        /// <summary>
        /// Whether the window position changed during the drag.
        /// </summary>
        private bool _moved;

        /// <summary>
        /// Whether the drag already ended and the handlers were detached.
        /// </summary>
        private bool _ended;

        /// <summary>
        /// Subscribes to the window events that drive the drag.
        /// </summary>
        public void Attach()
        {
            window.PointerMoved += OnPointerMoved;
            window.PointerReleased += OnPointerEnded;
            window.PointerCaptureLost += OnPointerEnded;
        }

        /// <summary>
        /// Moves the window so the pointer stays at the grabbed spot of the window.
        /// </summary>
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var cursor = window.PointToScreen(e.GetPosition(window));
            var position = WindowPlacement.DragPosition(cursor, grabFraction, ScreenHelper.GetPixelSize(window));
            if (position == window.Position)
            {
                return;
            }

            window.Position = position;
            _moved = true;
        }

        /// <summary>
        /// Detaches the handlers and reports the end of the drag.
        /// </summary>
        private void OnPointerEnded(object? sender, RoutedEventArgs e)
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            window.PointerMoved -= OnPointerMoved;
            window.PointerReleased -= OnPointerEnded;
            window.PointerCaptureLost -= OnPointerEnded;
            onEnded?.Invoke(_moved);
        }
    }
}
