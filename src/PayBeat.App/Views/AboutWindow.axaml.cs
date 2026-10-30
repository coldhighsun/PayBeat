using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PayBeat.App.Helpers;
using PayBeat.App.Services;

namespace PayBeat.App.Views;

/// <summary>
/// Displays app version, author, and license information, and checks for updates on open.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    /// Service used to persist and read the update-check throttle.
    /// </summary>
    private readonly SettingsService _settingsService;

    /// <summary>
    /// Release page opened by the "update available" link.
    /// </summary>
    private string? _updateUrl;

    /// <summary>
    /// Initializes the window for the XAML loader and designer, using the default settings location.
    /// </summary>
    public AboutWindow()
        : this(new SettingsService())
    {
    }

    /// <summary>
    /// Initializes the about window, populates the version label, and starts an update check.
    /// </summary>
    /// <param name="settingsService">Settings service used by the update check throttle.</param>
    public AboutWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        VersionText.Text = $"v{AppVersion.Current}";
        UpdateStatusText.Text = LocalizationService.Get("About.Update.Checking");
        PointerPressed += Window_PointerPressed;
        _ = CheckForUpdatesAsync();
    }

    /// <summary>
    /// Checks for a newer release and updates the status line or link accordingly.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        var info = await new UpdateCheckService(_settingsService).GetLatestReleaseAsync(bypassThrottle: true);

        if (info != null)
        {
            UpdateStatusText.IsVisible = false;
            _updateUrl = info.HtmlUrl;
            UpdateStatusLink.Content = string.Format(LocalizationService.Get("About.Update.Available"), info.Version);
            UpdateStatusLink.IsVisible = true;
        }
        else
        {
            UpdateStatusText.Text = LocalizationService.Get("About.Update.UpToDate");
        }
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Opens the project page stored in the clicked button's tag.
    /// </summary>
    private void GitHubLink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
        {
            ExternalLauncher.Open(url);
        }
    }

    /// <summary>
    /// Opens the release page of the available update.
    /// </summary>
    private void UpdateLink_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateUrl is { } url)
        {
            ExternalLauncher.Open(url);
        }
    }

    /// <summary>
    /// Lets the borderless window be dragged by its background.
    /// </summary>
    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        WindowDrag.TryBegin(this, e);
    }
}
