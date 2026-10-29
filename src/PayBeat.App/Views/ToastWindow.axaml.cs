using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PayBeat.App.Views;

/// <summary>
/// A small, non-activating, always-on-top notification card. Fades in when opened, fades out and
/// closes itself after a timeout (paused while the pointer hovers over it), and raises
/// <see cref="Clicked"/> when the user clicks its body.
/// </summary>
public partial class ToastWindow : Window
{
    /// <summary>
    /// How long the fade-out transition runs before the window is actually closed.
    /// </summary>
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(220);

    /// <summary>
    /// Timer that starts the fade-out once the toast's lifetime elapsed.
    /// </summary>
    private readonly DispatcherTimer _lifetimeTimer;

    /// <summary>
    /// Whether the toast is already fading out.
    /// </summary>
    private bool _closing;

    /// <summary>
    /// Initializes the XAML tree; used by the designer and the XAML loader.
    /// </summary>
    public ToastWindow()
        : this(string.Empty, string.Empty, TimeSpan.FromSeconds(6))
    {
    }

    /// <summary>
    /// Creates a toast.
    /// </summary>
    /// <param name="title">Toast title.</param>
    /// <param name="body">Toast body text.</param>
    /// <param name="lifetime">How long the toast stays before fading out.</param>
    public ToastWindow(string title, string body, TimeSpan lifetime)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;

        _lifetimeTimer = new DispatcherTimer { Interval = lifetime };
        _lifetimeTimer.Tick += (_, _) => BeginClose();

        Opened += (_, _) =>
        {
            Opacity = 1;
            _lifetimeTimer.Start();
        };
        PointerEntered += (_, _) => _lifetimeTimer.Stop();
        PointerExited += (_, _) =>
        {
            if (!_closing)
            {
                _lifetimeTimer.Start();
            }
        };
        PointerPressed += OnPointerPressed;
    }

    /// <summary>
    /// Raised when the user clicks the toast body (not the close button).
    /// </summary>
    public event Action? Clicked;

    /// <summary>
    /// Fades the toast out and closes it.
    /// </summary>
    private void BeginClose()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _lifetimeTimer.Stop();
        Opacity = 0;
        DispatcherTimer.RunOnce(Close, FadeOutDuration);
    }

    /// <summary>
    /// Closes the toast when its close button is pressed.
    /// </summary>
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => BeginClose();

    /// <summary>
    /// Raises <see cref="Clicked"/> and dismisses the toast when its body is clicked.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !e.Handled)
        {
            Clicked?.Invoke();
            BeginClose();
        }
    }
}
