using Avalonia.Controls;
using PayBeat.App.Helpers;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PayBeat.App.Views;

/// <summary>
/// Minimal themed message box replacing the WPF <c>MessageBox</c>. It is shown non-modally;
/// callers that must react to dismissal subscribe to <see cref="Window.Closed"/>.
/// </summary>
public partial class MessageWindow : Window
{
    /// <summary>
    /// Initializes the XAML tree; used by the designer and the XAML loader.
    /// </summary>
    public MessageWindow()
        : this(string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// Creates a message window.
    /// </summary>
    /// <param name="title">Window heading.</param>
    /// <param name="message">Message text.</param>
    public MessageWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        PointerPressed += OnPointerPressed;
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.Enter)
            {
                Close();
            }
        };
    }

    /// <summary>
    /// Closes the window when OK is pressed.
    /// </summary>
    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Lets the borderless window be dragged by its background.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        WindowDrag.TryBegin(this, e);
    }
}
