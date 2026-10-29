using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.Tests.Fakes;
using PayBeat.App.ViewModels;

namespace PayBeat.App.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="MainViewModel"/>. They run on the Avalonia UI thread because the view model owns dispatcher timers.
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    /// <summary>
    /// Temporary directory holding the settings file for each test instance.
    /// </summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"PayBeatTests-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the mode commands persist the new mode and refresh the convenience flags.
    /// </summary>
    [AvaloniaFact]
    public void SetMiniModeCommand_Executed_PersistsModeAndUpdatesFlags()
    {
        var settings = CreateSettings(new SalarySettings { DisplayMode = DisplayMode.Normal });
        using var vm = new MainViewModel(settings, new FakeWindowService());

        vm.SetMiniModeCommand.Execute(null);

        vm.DisplayMode.Should().Be(DisplayMode.Mini);
        vm.IsMiniMode.Should().BeTrue();
        vm.IsNormalMode.Should().BeFalse();
        settings.Load().DisplayMode.Should().Be(DisplayMode.Mini);
    }

    /// <summary>
    /// Verifies a mode change raises the property change the window listens to.
    /// </summary>
    [AvaloniaFact]
    public void SetFlexModeCommand_Executed_RaisesDisplayModeChanged()
    {
        var settings = CreateSettings(new SalarySettings { DisplayMode = DisplayMode.Normal });
        using var vm = new MainViewModel(settings, new FakeWindowService());
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SetFlexModeCommand.Execute(null);

        changed.Should().Contain(nameof(MainViewModel.DisplayMode));
        changed.Should().Contain(nameof(MainViewModel.IsFlexMode));
    }

    /// <summary>
    /// Verifies selecting the already active mode does nothing.
    /// </summary>
    [AvaloniaFact]
    public void SetNormalModeCommand_AlreadyNormal_DoesNotRaiseHotkeyChange()
    {
        var settings = CreateSettings(new SalarySettings { DisplayMode = DisplayMode.Normal });
        using var vm = new MainViewModel(settings, new FakeWindowService());
        var reloads = 0;
        vm.HotkeySettingsChanged += () => reloads++;

        vm.SetNormalModeCommand.Execute(null);

        reloads.Should().Be(0);
    }

    /// <summary>
    /// Verifies the formatted values use the configured currency and the current culture.
    /// </summary>
    [AvaloniaFact]
    public void FormattedProperties_CustomCurrency_UseCurrencyAndTwoDecimals()
    {
        var settings = CreateSettings(new SalarySettings { Currency = "$", DailySalary = 1234.5m });
        using var vm = new MainViewModel(settings, new FakeWindowService());

        vm.DailySalaryFormatted.Should().Be(1234.5m.ToString("N2"));
        vm.MiniWidthTemplate.Should().Be($"${1234.5m:N2}");
        vm.EarnedFormatted.Should().StartWith("$");
    }

    /// <summary>
    /// Verifies the window commands are forwarded to the window service.
    /// </summary>
    [AvaloniaFact]
    public void Commands_OpenSettingsAndExit_ForwardToWindowService()
    {
        var windows = new RecordingWindowService();
        using var vm = new MainViewModel(CreateSettings(new SalarySettings()), windows);

        vm.OpenSettingsCommand.Execute(null);
        vm.OpenAboutCommand.Execute(null);
        vm.ExitCommand.Execute(null);

        windows.Calls.Should().Equal("settings", "about", "shutdown");
    }

    /// <summary>
    /// Verifies the update notification carries the release URL.
    /// </summary>
    [AvaloniaFact]
    public void NotifyUpdateAvailable_WithUrl_RaisesNotificationWithUrl()
    {
        using var vm = new MainViewModel(CreateSettings(new SalarySettings()), new FakeWindowService());
        string? url = null;
        vm.NotificationRequested += (_, _, u) => url = u;

        vm.NotifyUpdateAvailable("2.0.0", "https://example.test/release");

        url.Should().Be("https://example.test/release");
    }

    /// <summary>
    /// Saves <paramref name="seed"/> to a temporary file and returns a service reading it.
    /// </summary>
    private SettingsService CreateSettings(SalarySettings seed)
    {
        var service = new SettingsService(Path.Combine(_dir, "settings.json"));
        service.Save(seed);
        return service;
    }

    /// <summary>
    /// Window service that records the order of calls.
    /// </summary>
    private sealed class RecordingWindowService : IWindowService
    {
        /// <summary>
        /// Gets the recorded calls.
        /// </summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc/>
        public bool IsHotkeySupported => true;

        /// <inheritdoc/>
        public void CloseSettings() => Calls.Add("close");

        /// <inheritdoc/>
        public void OpenAbout() => Calls.Add("about");

        /// <inheritdoc/>
        public void OpenSettings() => Calls.Add("settings");

        /// <inheritdoc/>
        public void ResumeHotkey() => Calls.Add("resume");

        /// <inheritdoc/>
        public void Shutdown() => Calls.Add("shutdown");

        /// <inheritdoc/>
        public void SuspendHotkey() => Calls.Add("suspend");
    }
}
