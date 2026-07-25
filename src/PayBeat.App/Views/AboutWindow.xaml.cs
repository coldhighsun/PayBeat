using PayBeat.App.Helpers;
using PayBeat.App.Services;
using System.Diagnostics;
using System.Windows.Navigation;

namespace PayBeat.App.Views;

/// <summary>
/// Displays app version, author, and license information, and checks for updates on open.
/// </summary>
public partial class AboutWindow
{
    private readonly SettingsService _settingsService;

    /// <summary>
    /// Initializes the about window, populates the version label, and starts an update check.
    /// </summary>
    public AboutWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        VersionText.Text = $"v{AppVersion.Current}";
        UpdateStatusText.Text = LocalizationService.Get("About.Update.Checking");
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        var info = await new UpdateCheckService().GetLatestReleaseAsync();
        _settingsService.Save(_settingsService.Load() with
        {
            LastUpdateCheckUtc = DateTimeOffset.UtcNow
        });

        if (info != null)
        {
            UpdateStatusText.Text = string.Empty;
            UpdateStatusLink.NavigateUri = new Uri(info.HtmlUrl);
            UpdateStatusLink.Inlines.Clear();
            UpdateStatusLink.Inlines.Add(string.Format(LocalizationService.Get("About.Update.Available"), info.Version));
            UpdateStatusLinkContainer.Visibility = Visibility.Visible;
        }
        else
        {
            UpdateStatusText.Text = LocalizationService.Get("About.Update.UpToDate");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}