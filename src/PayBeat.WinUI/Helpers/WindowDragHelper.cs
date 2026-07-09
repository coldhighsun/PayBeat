using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Provides a simple way to make a WinUI3 window draggable by clicking and dragging on a specified root element,
/// while ignoring interactive controls like buttons and text boxes.
/// </summary>
public static class WindowDragHelper
{
    private const int Threshold = 4;

    public static void Attach(FrameworkElement root, AppWindow appWindow)
    {
        var pressed = false;
        var dragging = false;
        POINT start = default;
        POINT last = default;

        root.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(root).Properties.IsLeftButtonPressed || IsOnInteractiveControl(e.OriginalSource as DependencyObject, root))
            {
                return;
            }

            pressed = true;
            GetCursorPos(out start);
            last = start;
        };

        root.PointerMoved += (_, e) =>
        {
            if (!pressed)
            {
                return;
            }

            GetCursorPos(out var current);

            if (!dragging)
            {
                if (Math.Abs(current.X - start.X) < Threshold && Math.Abs(current.Y - start.Y) < Threshold)
                {
                    return;
                }

                dragging = true;
                root.CapturePointer(e.Pointer);
            }

            var dx = current.X - last.X;
            var dy = current.Y - last.Y;
            last = current;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            var position = appWindow.Position;
            appWindow.Move(new PointInt32(position.X + dx, position.Y + dy));
        };

        root.PointerReleased += (_, e) =>
        {
            pressed = false;
            if (dragging)
            {
                dragging = false;
                root.ReleasePointerCapture(e.Pointer);
            }
        };

        root.PointerCaptureLost += (_, _) =>
        {
            pressed = false;
            dragging = false;
        };
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    private static bool IsOnInteractiveControl(DependencyObject? node, FrameworkElement root)
    {
        while (node is not null && node != root)
        {
            if (node is ButtonBase or TextBox or PasswordBox or Slider or ComboBox or ToggleSwitch or DatePicker or TimePicker or NumberBox or Selector)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}