using AwesomeAssertions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using GitHubReleaseUpdater.Assets;
using GitHubReleaseUpdater.Installation;
using GitHubReleaseUpdater.Versioning;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Services;

/// <summary>
/// Tests of <see cref="UpdateService"/> against a GitHub that is replaced by canned answers.
/// </summary>
public sealed class UpdateServiceTests : IDisposable
{
    /// <summary>
    /// The setup program contents served for the release.
    /// </summary>
    private static readonly byte[] Package = Encoding.UTF8.GetBytes("pretend this is a setup program");

    /// <summary>
    /// A directory the settings file and downloads of one test go to.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "paybeat-update-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The canned GitHub.
    /// </summary>
    private readonly FakeGitHub _github = new();

    /// <summary>
    /// The stand-in for the setup program that records what was started.
    /// </summary>
    private readonly FakeLauncher _launcher = new();

    /// <summary>
    /// The services a test created, disposed when it ends so that no background loop outlives it.
    /// </summary>
    private readonly List<UpdateService> _services = [];

    /// <summary>
    /// Settings service backing the last-check store.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateServiceTests"/> class.
    /// </summary>
    public UpdateServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _settings = new SettingsService(Path.Combine(_directory, "settings.json"));
    }

    /// <inheritdoc />
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
            // A background loop may still hold a file for a moment; the temp directory is cleaned up by the OS.
        }
    }

    /// <summary>
    /// A release with a higher version is reported as available, with a setup program for this platform.
    /// </summary>
    [Fact]
    public async Task CheckAsync_NewerRelease_IsAvailable()
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = Create("1.0.0");

        await service.CheckAsync(manual: false);

        service.Status.Should().Be(UpdateStatus.Available);
        service.LatestVersionText.Should().Be("9.9.9");
        service.HasPackage.Should().BeTrue();
        service.CanDownload.Should().BeTrue();
        service.ReleaseUrl.Should().NotBeNull();
    }

    /// <summary>
    /// A release with the running version is not an update.
    /// </summary>
    [Fact]
    public async Task CheckAsync_SameVersion_IsUpToDate()
    {
        _github.Latest = FakeGitHub.Release("v1.0.0", withPackage: true);
        var service = Create("1.0.0");

        await service.CheckAsync(manual: false);

        service.Status.Should().Be(UpdateStatus.UpToDate);
        service.CanDownload.Should().BeFalse();
        service.LatestVersion.Should().BeNull();
    }

    /// <summary>
    /// A newer release without a setup program is still known, but cannot be downloaded.
    /// </summary>
    [Fact]
    public async Task CheckAsync_ReleaseWithoutPackage_CannotBeDownloaded()
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: false);
        var service = Create("1.0.0");

        await service.CheckAsync(manual: false);

        service.Status.Should().Be(UpdateStatus.Available);
        service.HasPackage.Should().BeFalse();
        service.CanDownload.Should().BeFalse();
    }

    /// <summary>
    /// Where the platform cannot install, a newer release is reported but never offered for download.
    /// </summary>
    [Fact]
    public async Task CanDownload_PlatformCannotInstall_IsFalse()
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = Create("1.0.0", useSetup: false);

        await service.CheckAsync(manual: false);

        service.Status.Should().Be(UpdateStatus.Available);
        service.CanDownload.Should().BeFalse();
    }

    /// <summary>
    /// A failed check is shown as a failure only when the user asked for it.
    /// </summary>
    [Fact]
    public async Task CheckAsync_Failure_IsReportedOnlyWhenManual()
    {
        _github.Latest = null;
        var service = Create("1.0.0");

        await service.CheckAsync(manual: false);
        service.Status.Should().Be(UpdateStatus.Idle);

        await service.CheckAsync(manual: true);
        service.Status.Should().Be(UpdateStatus.Failed);
    }

    /// <summary>
    /// An automatic check right after another one is skipped, and the time of the last one survives a restart.
    /// </summary>
    [Fact]
    public async Task CheckAsync_AutomaticWithinInterval_IsThrottledAcrossInstances()
    {
        _github.Latest = FakeGitHub.Release("v1.0.0", withPackage: true);

        await Create("1.0.0").CheckAsync(manual: false);
        await Create("1.0.0").CheckAsync(manual: false);

        _github.ApiRequests.Should().Be(1);
        _settings.Load().LastUpdateCheckUtc.Should().NotBeNull();
    }

    /// <summary>
    /// A failed check is throttled like a successful one, so an offline machine does not retry every round.
    /// </summary>
    [Fact]
    public async Task CheckAsync_AutomaticFailure_IsThrottled()
    {
        _github.Latest = null;

        await Create("1.0.0").CheckAsync(manual: false);
        await Create("1.0.0").CheckAsync(manual: false);

        _github.ApiRequests.Should().Be(1);
    }

    /// <summary>
    /// A check the user asks for ignores the interval.
    /// </summary>
    [Fact]
    public async Task CheckAsync_Manual_IgnoresInterval()
    {
        _github.Latest = FakeGitHub.Release("v1.0.0", withPackage: true);
        var service = Create("1.0.0");

        await service.CheckAsync(manual: false);
        await service.CheckAsync(manual: true);

        _github.ApiRequests.Should().Be(2);
    }

    /// <summary>
    /// A build that is not official does not check by itself, and neither does one with an unknown version.
    /// </summary>
    [Theory]
    [InlineData("1.0.0", false)]
    [InlineData(null, true)]
    public async Task Start_CannotAutoCheck_DoesNothing(string? current, bool officialBuild)
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = Create(current, officialBuild: officialBuild, firstCheckDelay: TimeSpan.Zero);

        service.CanAutoCheck.Should().BeFalse();
        service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        _github.ApiRequests.Should().Be(0);
    }

    /// <summary>
    /// Where the platform cannot install, a newer release is still announced so that the user is told about it.
    /// </summary>
    [Fact]
    public async Task Start_PlatformCannotInstall_StillAnnounces()
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = Create("1.0.0", useSetup: false, firstCheckDelay: TimeSpan.Zero);
        var announced = 0;
        service.UpdateFound += (_, _) => announced++;

        service.Start();
        await WaitUntilAsync(() => service.Status == UpdateStatus.Available);

        announced.Should().Be(1);
    }

    /// <summary>
    /// Only a folder with the uninstaller the setup program writes counts as an installed copy.
    /// </summary>
    [Fact]
    public void IsSetupInstall_DependsOnTheUninstaller()
    {
        UpdateService.IsSetupInstall(_directory).Should().BeFalse();

        File.WriteAllText(Path.Combine(_directory, "unins000.exe"), string.Empty);

        UpdateService.IsSetupInstall(_directory).Should().BeTrue();
    }

    /// <summary>
    /// An official build checks by itself shortly after <see cref="UpdateService.Start"/> and announces what it finds.
    /// </summary>
    [Fact]
    public async Task Start_OfficialBuild_ChecksAndAnnouncesOnce()
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = Create("1.0.0", firstCheckDelay: TimeSpan.Zero);
        var announced = 0;
        service.UpdateFound += (_, _) => announced++;

        service.Start();
        service.Start();
        await WaitUntilAsync(() => service.Status == UpdateStatus.Available);

        announced.Should().Be(1);
    }

    /// <summary>
    /// The setup program is downloaded and verified against the checksum file.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ValidChecksum_ReachesDownloaded()
    {
        var service = await CreateAvailableAsync();

        await service.DownloadAsync();

        service.Status.Should().Be(UpdateStatus.Downloaded);
        service.CanInstall.Should().BeTrue();
        File.ReadAllBytes(FindDownloaded()).Should().Equal(Package);
    }

    /// <summary>
    /// A package that does not match the checksum file is refused and nothing can be installed.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ChecksumMismatch_Fails()
    {
        _github.CorruptPackage = true;
        var service = await CreateAvailableAsync();

        await service.DownloadAsync();

        service.Status.Should().Be(UpdateStatus.Failed);
        service.CanInstall.Should().BeFalse();
    }

    /// <summary>
    /// The downloaded setup program is started silently, closes the app and starts it again.
    /// </summary>
    [Fact]
    public async Task InstallAsync_DownloadedSetup_StartsItSilentlyWithRelaunch()
    {
        var service = await CreateDownloadedAsync();

        await service.InstallAsync();

        var started = _launcher.Started.Should().ContainSingle().Subject;
        started.Options.Arguments.Should().Contain("/SILENT")
            .And.Contain(UpdateService.CloseApplicationsArgument)
            .And.Contain(UpdateService.RelaunchArgument);
        started.Options.RequireVerified.Should().BeTrue();
    }

    /// <summary>
    /// Installing before anything was downloaded does nothing.
    /// </summary>
    [Fact]
    public async Task InstallAsync_NothingDownloaded_StartsNothing()
    {
        var service = await CreateAvailableAsync();

        await service.InstallAsync();

        _launcher.Started.Should().BeEmpty();
    }

    /// <summary>
    /// The prepare hook runs before the setup program is started, and a failing hook does not stop the install.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallAsync_PrepareHook_RunsBeforeTheSetupProgramStarts(bool prepareFails)
    {
        var service = await CreateDownloadedAsync();
        var startedWhenPrepared = -1;
        service.PrepareForInstallAsync = () =>
        {
            startedWhenPrepared = _launcher.Started.Count;
            return prepareFails ? Task.FromException(new IOException("disk full")) : Task.CompletedTask;
        };

        await service.InstallAsync();

        startedWhenPrepared.Should().Be(0);
        _launcher.Started.Should().ContainSingle();
    }

    /// <summary>
    /// A prepare hook that never ends is given up on after the timeout and the install goes ahead.
    /// </summary>
    [Fact]
    public async Task InstallAsync_PrepareHookNeverEnds_StartsTheSetupProgramAfterTheTimeout()
    {
        var service = await CreateDownloadedAsync();
        service.PrepareForInstallAsync = () => new TaskCompletionSource().Task;

        await service.InstallAsync();

        _launcher.Started.Should().ContainSingle();
    }

    /// <summary>
    /// A setup program that cannot be started is shown as a failure and the package stays ready for another try.
    /// </summary>
    [Fact]
    public async Task InstallAsync_LaunchFails_ShowsFailureAndKeepsPackage()
    {
        var service = await CreateDownloadedAsync();
        _launcher.Failure = new InvalidOperationException("elevation refused");

        await service.InstallAsync();

        service.Status.Should().Be(UpdateStatus.Failed);
        service.CanInstall.Should().BeTrue();
    }

    /// <summary>
    /// The official-build flag is read from the assembly metadata, and is false for a build without it.
    /// </summary>
    [Fact]
    public void ReadOfficialBuild_TestAssembly_IsFalse()
    {
        UpdateService.ReadOfficialBuild(typeof(UpdateServiceTests).Assembly).Should().BeFalse();
    }

    /// <summary>
    /// Waits until a condition holds, for at most five seconds, and fails the test if it does not.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold in time.");
            }

            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Creates a service that has found the newer release.
    /// </summary>
    private async Task<UpdateService> CreateAvailableAsync()
    {
        _github.Latest = FakeGitHub.Release("v9.9.9", withPackage: true);
        var service = Create("1.0.0");
        await service.CheckAsync(manual: false);
        return service;
    }

    /// <summary>
    /// Creates a service that has found and downloaded the newer release.
    /// </summary>
    private async Task<UpdateService> CreateDownloadedAsync()
    {
        var service = await CreateAvailableAsync();
        await service.DownloadAsync();
        return service;
    }

    /// <summary>
    /// Finds the file the setup program was downloaded to.
    /// </summary>
    private string FindDownloaded() =>
        Directory.GetFiles(Path.Combine(_directory, "updates"), "*-setup-*.exe", SearchOption.AllDirectories).Single();

    /// <summary>
    /// Creates the service under test against the canned GitHub.
    /// </summary>
    private UpdateService Create(
        string? current,
        bool officialBuild = true,
        bool useSetup = true,
        TimeSpan? firstCheckDelay = null)
    {
        var service = new UpdateService(
            new SettingsLastCheckStore(_settings),
            current is null ? null : SemanticVersion.Parse(current),
            officialBuild,
            Path.Combine(_directory, "updates"),
            useSetup,
            new HttpClient(_github, disposeHandler: false),
            _launcher,
            prepareTimeout: TimeSpan.FromMilliseconds(200),
            installerGracePeriod: TimeSpan.Zero,
            firstCheckDelay: firstCheckDelay);
        _services.Add(service);
        return service;
    }

    /// <summary>
    /// A stand-in for starting the setup program that records what would have been started.
    /// </summary>
    internal sealed class FakeLauncher : IInstallerLauncher
    {
        /// <summary>
        /// Gets the setup programs that were started.
        /// </summary>
        public List<(string Path, InstallerLaunchOptions Options)> Started
        {
            get;
        } = [];

        /// <summary>
        /// Gets or sets the failure to throw instead of starting, or <see langword="null"/>.
        /// </summary>
        public Exception? Failure
        {
            get; set;
        }

        /// <inheritdoc />
        public InstallerLaunchResult Launch(string installerPath, InstallerLaunchOptions options)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Started.Add((installerPath, options));
            return new InstallerLaunchResult(installerPath, null);
        }
    }

    /// <summary>
    /// A stand-in for the GitHub API and the download host.
    /// </summary>
    internal sealed class FakeGitHub : HttpMessageHandler
    {
        /// <summary>
        /// Gets or sets the JSON of the latest release, or <see langword="null"/> to make the API fail.
        /// </summary>
        public string? Latest
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the package differs from what the checksum file says.
        /// </summary>
        public bool CorruptPackage
        {
            get; set;
        }

        /// <summary>
        /// Gets how many times the release API was asked.
        /// </summary>
        public int ApiRequests
        {
            get; private set;
        }

        /// <summary>
        /// Builds the JSON of a release.
        /// </summary>
        /// <param name="tag">The tag of the release.</param>
        /// <param name="withPackage">Whether the release carries the setup program of this platform and the checksum file.</param>
        /// <returns>The JSON.</returns>
        public static string Release(string tag, bool withPackage)
        {
            var name = $"PayBeat-{tag}-setup-{RuntimeInfo.Current.Rid}.exe";
            var assets = withPackage
                ? $$"""
                    [
                      { "id": 1, "name": "{{name}}", "size": {{Package.Length}}, "browser_download_url": "https://example.test/dl/{{name}}", "url": "https://example.test/dl/{{name}}" },
                      { "id": 2, "name": "SHA256SUMS.txt", "size": 100, "browser_download_url": "https://example.test/dl/SHA256SUMS.txt", "url": "https://example.test/dl/SHA256SUMS.txt" }
                    ]
                    """
                : "[]";
            return $$"""
                { "id": 10, "tag_name": "{{tag}}", "name": "{{tag}}", "draft": false, "prerelease": false, "html_url": "https://github.com/coldhighsun/PayBeat/releases/tag/{{tag}}", "body": "", "assets": {{assets}} }
                """;
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/releases/latest", StringComparison.Ordinal))
            {
                ApiRequests++;
                return Task.FromResult(Latest is null
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Latest, Encoding.UTF8, "application/json") });
            }

            if (path.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal))
            {
                var hash = Convert.ToHexStringLower(SHA256.HashData(Package));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{hash}  PayBeat-v9.9.9-setup-{RuntimeInfo.Current.Rid}.exe\n"),
                });
            }

            if (path.EndsWith(".exe", StringComparison.Ordinal))
            {
                var bytes = CorruptPackage ? [.. Package, (byte)0] : Package;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
