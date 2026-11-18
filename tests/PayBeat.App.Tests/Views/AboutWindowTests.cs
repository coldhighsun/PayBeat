using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using AwesomeAssertions;
using GitHubReleaseUpdater.Versioning;
using PayBeat.App.Services;
using PayBeat.App.Tests.Services;
using PayBeat.App.Views;

namespace PayBeat.App.Tests.Views;

/// <summary>
/// Tests of the update section of <see cref="AboutWindow"/>, against an <see cref="UpdateService"/> that talks to a
/// canned GitHub.
/// </summary>
public sealed class AboutWindowTests : IDisposable
{
    /// <summary>
    /// A directory the settings file and downloads of one test go to.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "paybeat-about-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The canned GitHub.
    /// </summary>
    private readonly UpdateServiceTests.FakeGitHub _github = new();

    /// <summary>
    /// The stand-in for the setup program that records what was started.
    /// </summary>
    private readonly UpdateServiceTests.FakeLauncher _launcher = new();

    /// <summary>
    /// The services a test created, disposed when it ends.
    /// </summary>
    private readonly List<UpdateService> _services = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var service in _services)
        {
            service.Dispose();
        }

        _github.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A background task may still hold a file for a moment; the temp directory is cleaned up by the OS.
        }
    }

    /// <summary>
    /// Only a state in which nothing is known, nothing newer was found or the last attempt failed triggers a check when
    /// the window opens.
    /// </summary>
    [Theory]
    [InlineData(UpdateStatus.Idle, true)]
    [InlineData(UpdateStatus.UpToDate, true)]
    [InlineData(UpdateStatus.Failed, true)]
    [InlineData(UpdateStatus.Checking, false)]
    [InlineData(UpdateStatus.Available, false)]
    [InlineData(UpdateStatus.Downloading, false)]
    [InlineData(UpdateStatus.Downloaded, false)]
    [InlineData(UpdateStatus.Installing, false)]
    public void ShouldCheckOnOpen_Status_MatchesExpectation(UpdateStatus status, bool expected)
    {
        AboutWindow.ShouldCheckOnOpen(status).Should().Be(expected);
    }

    /// <summary>
    /// When the running version is the newest one, the status line says so and no link or button is shown.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_UpToDate_ShowsStatusTextOnly()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v1.0.0", withPackage: true);
        var window = new AboutWindow(CreateService(useSetup: true));

        await WaitUntilAsync(() => window.UpdateStatusText.Text == LocalizationService.Get("About.Update.UpToDate"));

        window.UpdateStatusText.IsVisible.Should().BeTrue();
        window.UpdateStatusLink.IsVisible.Should().BeFalse();
        window.UpdateNowButton.IsVisible.Should().BeFalse();
        window.Close();
    }

    /// <summary>
    /// Where updates cannot be installed, a newer version is shown as a link to the release page and has no button.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_UpdateAvailableButPlatformCannotInstall_ShowsLink()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: true);
        var window = new AboutWindow(CreateService(useSetup: false));

        await WaitUntilAsync(() => window.UpdateStatusLink.IsVisible);

        window.UpdateStatusText.IsVisible.Should().BeFalse();
        window.UpdateNowButton.IsVisible.Should().BeFalse();
        window.UpdateStatusLink.Content.Should().Be(
            string.Format(LocalizationService.Get("About.Update.Available"), "9.9.9"));
        window.Close();
    }

    /// <summary>
    /// Where updates can be installed, a newer version is shown with the update button instead of the link.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_UpdateAvailableAndInstallable_ShowsButton()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: true);
        var window = new AboutWindow(CreateService(useSetup: true));

        await WaitUntilAsync(() => window.UpdateNowButton.IsVisible);

        window.UpdateStatusLink.IsVisible.Should().BeFalse();
        window.UpdateStatusText.IsVisible.Should().BeTrue();
        window.UpdateStatusText.Text.Should().Be(
            string.Format(LocalizationService.Get("About.Update.Available"), "9.9.9"));
        window.Close();
    }

    /// <summary>
    /// A release without a setup program for this platform has no update button, only the link.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_UpdateWithoutPackage_ShowsLinkAndNoButton()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: false);
        var window = new AboutWindow(CreateService(useSetup: true));

        await WaitUntilAsync(() => window.UpdateStatusLink.IsVisible);

        window.UpdateNowButton.IsVisible.Should().BeFalse();
        window.Close();
    }

    /// <summary>
    /// Opening the window while a package is already downloaded does not check again, so the downloaded state and its
    /// install button survive.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_PackageAlreadyDownloaded_DoesNotCheckAgain()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = CreateService(useSetup: true);
        await service.CheckAsync(manual: false);
        await service.DownloadAsync();
        var requestsBefore = _github.ApiRequests;

        var window = new AboutWindow(service);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        _github.ApiRequests.Should().Be(requestsBefore);
        service.Status.Should().Be(UpdateStatus.Downloaded);
        window.UpdateNowButton.IsVisible.Should().BeTrue();
        window.UpdateStatusText.Text.Should().Be(LocalizationService.Get("About.Update.Downloaded"));
        window.Close();
    }

    /// <summary>
    /// Clicking the update button downloads the package and starts the setup program, and the button can be used again
    /// afterwards.
    /// </summary>
    [AvaloniaFact]
    public async Task UpdateNowClick_UpdateAvailable_DownloadsAndStartsSetup()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = CreateService(useSetup: true);
        var window = new AboutWindow(service);
        await WaitUntilAsync(() => window.UpdateNowButton.IsVisible);

        window.UpdateNowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => _launcher.Started.Count == 1 && window.UpdateNowButton.IsEnabled);

        _launcher.Started.Should().ContainSingle();
        window.Close();
    }

    /// <summary>
    /// A setup program that cannot be started is shown as a failure and the update button stays available for a retry,
    /// without the exception escaping the click handler.
    /// </summary>
    [AvaloniaFact]
    public async Task UpdateNowClick_LaunchFails_ShowsFailureAndKeepsButton()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: true);
        _launcher.Failure = new InvalidOperationException("elevation refused");
        var service = CreateService(useSetup: true);
        var window = new AboutWindow(service);
        await WaitUntilAsync(() => window.UpdateNowButton.IsVisible);

        window.UpdateNowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() =>
            window.UpdateStatusText.Text == LocalizationService.Get("About.Update.Failed") && window.UpdateNowButton.IsEnabled);

        window.UpdateNowButton.IsVisible.Should().BeTrue();
        _launcher.Started.Should().BeEmpty();
        window.Close();
    }

    /// <summary>
    /// A window that was given the shared service only stops listening when it closes and leaves the service usable.
    /// </summary>
    [AvaloniaFact]
    public async Task Close_SharedService_StaysUsable()
    {
        _github.Latest = UpdateServiceTests.FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = CreateService(useSetup: true);
        var window = new AboutWindow(service);
        await WaitUntilAsync(() => window.UpdateNowButton.IsVisible);

        window.Close();
        await service.CheckAsync(manual: true);

        service.Status.Should().Be(UpdateStatus.Available);
    }

    /// <summary>
    /// Waits until a condition holds, for at most five seconds, and fails the test if it does not.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold in time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Creates a service for version 1.0.0 against the canned GitHub.
    /// </summary>
    /// <param name="useSetup">Whether the platform installs updates.</param>
    /// <returns>The service, disposed when the test ends.</returns>
    private UpdateService CreateService(bool useSetup)
    {
        Directory.CreateDirectory(_directory);
        var service = new UpdateService(
            new SettingsLastCheckStore(new SettingsService(Path.Combine(_directory, "settings.json"))),
            SemanticVersion.Parse("1.0.0"),
            officialBuild: true,
            Path.Combine(_directory, "updates"),
            useSetup,
            new HttpClient(_github, disposeHandler: false),
            _launcher,
            prepareTimeout: TimeSpan.FromMilliseconds(200),
            installerGracePeriod: TimeSpan.Zero);
        _services.Add(service);
        return service;
    }
}
