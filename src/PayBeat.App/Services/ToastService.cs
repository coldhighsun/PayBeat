using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PayBeat.App.Helpers;
using PayBeat.App.ViewModels;
using PayBeat.App.Views;

namespace PayBeat.App.Services;

/// <summary>
/// Shows in-app toast notifications (milestone, end-of-day and update reminders) as small
/// borderless windows stacked in the bottom-right corner of the primary screen's work area.
/// Replaces the Windows-only tray balloon tip so notifications look and behave the same everywhere.
/// </summary>
public sealed class ToastService : IDisposable
{
    /// <summary>
    /// How long a toast stays on screen before fading out.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Gap between a toast and the work area edges, in device-independent units.
    /// </summary>
    private const int MarginDips = 12;

    /// <summary>
    /// Gap between stacked toasts, in device-independent units.
    /// </summary>
    private const int GapDips = 8;

    /// <summary>
    /// View model whose <see cref="MainViewModel.NotificationRequested"/> event drives the toasts.
    /// </summary>
    private readonly MainViewModel _viewModel;

    /// <summary>
    /// Callback used to open a toast's URL when it is clicked.
    /// </summary>
    private readonly Action<string> _openUrl;

    /// <summary>
    /// Toasts currently on screen, oldest first.
    /// </summary>
    private readonly List<ToastWindow> _toasts = [];

    /// <summary>
    /// Creates the service and starts listening for notification requests.
    /// </summary>
    /// <param name="viewModel">Source of notification requests.</param>
    /// <param name="openUrl">Invoked with the toast's URL when a toast that carries one is clicked.</param>
    public ToastService(MainViewModel viewModel, Action<string> openUrl)
    {
        _viewModel = viewModel;
        _openUrl = openUrl;
        _viewModel.NotificationRequested += OnNotificationRequested;
    }

    /// <summary>
    /// Closes every toast that is currently visible, e.g. when the widget is hidden via the hotkey.
    /// </summary>
    public void DismissAll()
    {
        foreach (var toast in _toasts.ToList())
        {
            toast.Close();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModel.NotificationRequested -= OnNotificationRequested;
        DismissAll();
    }

    /// <summary>
    /// Creates and shows a toast for a notification request.
    /// </summary>
    /// <param name="title">Toast title.</param>
    /// <param name="body">Toast body text.</param>
    /// <param name="url">Optional URL opened when the toast is clicked.</param>
    private void OnNotificationRequested(string title, string body, string? url)
    {
        Dispatcher.UIThread.Post(() => Show(title, body, url));
    }

    /// <summary>
    /// Shows a toast on the UI thread and wires up its lifetime handling.
    /// </summary>
    private void Show(string title, string body, string? url)
    {
        var toast = new ToastWindow(title, body, Lifetime);
        toast.Clicked += () =>
        {
            if (url is not null)
            {
                _openUrl(url);
            }
        };
        toast.Opened += (_, _) => Relayout();
        toast.Closed += (_, _) =>
        {
            _toasts.Remove(toast);
            Relayout();
        };

        _toasts.Add(toast);
        toast.Show();
    }

    /// <summary>
    /// Repositions all visible toasts into a bottom-right stack on the primary screen.
    /// </summary>
    private void Relayout()
    {
        if (_toasts.Count == 0)
        {
            return;
        }

        var screen = _toasts[0].Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scale = screen.Scaling;
        var sizes = _toasts.Select(ScreenHelper.GetPixelSize).ToList();
        var positions = ToastLayout.Stack(screen.WorkingArea, sizes, (int)(MarginDips * scale), (int)(GapDips * scale));
        for (var i = 0; i < _toasts.Count; i++)
        {
            _toasts[i].Position = positions[i];
        }
    }
}
