using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitHubReleaseUpdater.Versioning;
using PayBeat.App.Helpers;
using PayBeat.App.Services;

namespace PayBeat.App.Views;

/// <summary>
/// Displays app version, author, and license information, and checks for updates on open. On Windows the
/// update can be downloaded and installed from here.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    /// Service that checks for, downloads and installs updates.
    /// </summary>
    private readonly UpdateService _updateService;

    /// <summary>
    /// Whether this window created <see cref="_updateService"/> itself and must dispose it on close.
    /// </summary>
    private readonly bool _ownsUpdateService;

    /// <summary>
    /// Initializes the window for the XAML loader, designer and tests, with its own update service that uses the
    /// default settings location.
    /// </summary>
    public AboutWindow()
        : this(CreateStandaloneService(), ownsUpdateService: true)
    {
    }

    /// <summary>
    /// Initializes the about window with the app's shared update service.
    /// </summary>
    /// <param name="updateService">Service that checks for, downloads and installs updates.</param>
    public AboutWindow(UpdateService updateService)
        : this(updateService, ownsUpdateService: false)
    {
    }

    /// <summary>
    /// Initializes the about window, populates the version label, and starts an update check.
    /// </summary>
    /// <param name="updateService">Service that checks for, downloads and installs updates.</param>
    /// <param name="ownsUpdateService">Whether the window disposes the service when it closes.</param>
    private AboutWindow(UpdateService updateService, bool ownsUpdateService)
    {
        _updateService = updateService;
        _ownsUpdateService = ownsUpdateService;
        InitializeComponent();
        VersionText.Text = $"v{AppVersion.Current}";
        PointerPressed += Window_PointerPressed;
        _updateService.Changed += UpdateService_Changed;
        Closed += (_, _) =>
        {
            _updateService.Changed -= UpdateService_Changed;
            if (_ownsUpdateService)
            {
                _updateService.Dispose();
            }
        };
        RefreshUpdateStatus();
        if (ShouldCheckOnOpen(_updateService.Status))
        {
            _ = CheckForUpdatesAsync();
        }
    }

    /// <summary>
    /// Tells whether opening the window should ask GitHub again. A check that is running, a release that is already
    /// known, and a download or install in progress are left alone: a manual check ignores the check interval and
    /// would reset that state.
    /// </summary>
    /// <param name="status">
    /// The current update status.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when nothing is known yet, the last check found nothing newer, or the last attempt failed.
    /// </returns>
    internal static bool ShouldCheckOnOpen(UpdateStatus status) =>
        status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Failed;

    /// <summary>
    /// Creates the update service used when the window is opened without the app's shared one.
    /// </summary>
    private static UpdateService CreateStandaloneService() =>
        new(
            new SettingsLastCheckStore(new SettingsService()),
            SemanticVersion.TryFromAssembly(typeof(AboutWindow).Assembly, out var version) ? version : null,
            UpdateService.ReadOfficialBuild(typeof(AboutWindow).Assembly),
            UpdateService.DefaultDownloadDirectory(),
            useSetup: OperatingSystem.IsWindows() && UpdateService.IsSetupInstall());

    /// <summary>
    /// Checks for a newer release; the status line follows through <see cref="UpdateService.Changed"/>.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            await _updateService.CheckAsync(manual: true);
        }
        catch (Exception ex)
        {
            // Nothing awaits this task; the status line keeps showing the last known state.
            Debug.WriteLine($"The update check failed: {ex}");
        }

        RefreshUpdateStatus();
    }

    /// <summary>
    /// Re-renders the status line, link and button on the UI thread whenever the update state changes.
    /// </summary>
    private void UpdateService_Changed(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RefreshUpdateStatus);

    /// <summary>
    /// Shows the status line, the release link and the install button that match the current update state.
    /// </summary>
    private void RefreshUpdateStatus()
    {
        var status = _updateService.Status;
        var canInstall = _updateService.CanDownload || _updateService.CanInstall;
        var text = status switch
        {
            UpdateStatus.UpToDate => LocalizationService.Get("About.Update.UpToDate"),
            UpdateStatus.Available => string.Format(
                LocalizationService.Get("About.Update.Available"), _updateService.LatestVersionText),
            UpdateStatus.Downloading => string.Format(
                LocalizationService.Get("About.Update.Downloading"),
                _updateService.DownloadPercent is { } percent ? $"{(int)percent}%" : "…"),
            UpdateStatus.Downloaded => LocalizationService.Get("About.Update.Downloaded"),
            UpdateStatus.Installing => LocalizationService.Get("About.Update.Installing"),
            UpdateStatus.Failed => LocalizationService.Get("About.Update.Failed"),
            UpdateStatus.Checking => LocalizationService.Get("About.Update.Checking"),
            _ => string.Empty,
        };

        // Where the update cannot be installed (other platforms, a portable copy, or a release without a package) the
        // status is a link to the release page.
        var showLink = !canInstall
            && _updateService.ReleaseUrl is not null
            && status is UpdateStatus.Available;
        UpdateStatusText.Text = text;
        UpdateStatusText.IsVisible = !showLink;
        UpdateStatusLink.Content = text;
        UpdateStatusLink.IsVisible = showLink;
        UpdateNowButton.IsVisible = canInstall;
    }

    /// <summary>
    /// Downloads the update if needed and starts its installer, which closes this app and starts the new version.
    /// </summary>
    private async void UpdateNowButton_Click(object? sender, RoutedEventArgs e)
    {
        UpdateNowButton.IsEnabled = false;
        try
        {
            if (_updateService.CanDownload)
            {
                await _updateService.DownloadAsync();
            }

            if (_updateService.CanInstall)
            {
                await _updateService.InstallAsync();
            }
        }
        catch (Exception ex)
        {
            // An async void handler must not let an exception escape: it would be raised on the UI thread and end the app.
            Debug.WriteLine($"The update failed: {ex}");
        }
        finally
        {
            UpdateNowButton.IsEnabled = true;
            RefreshUpdateStatus();
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
        if (_updateService.ReleaseUrl is { } url)
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
