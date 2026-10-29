using AwesomeAssertions;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.Tests.Fakes;
using PayBeat.App.ViewModels;

namespace PayBeat.App.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="SettingsViewModel"/> validation and saving.
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    /// <summary>
    /// Temporary directory holding the settings file for each test instance.
    /// </summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"PayBeatTests-{Guid.NewGuid():N}");

    /// <summary>
    /// Settings service bound to the temporary directory.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Records reload requests.
    /// </summary>
    private readonly FakeSettingsApplier _applier = new();

    /// <summary>
    /// Fake startup registration.
    /// </summary>
    private readonly FakeStartupService _startup = new();

    /// <summary>
    /// Records window requests.
    /// </summary>
    private readonly FakeWindowService _windows = new();

    /// <summary>
    /// Records the language and theme that were applied.
    /// </summary>
    private readonly FakeAppearanceService _appearance = new();

    /// <summary>
    /// Initializes the fixture with default settings.
    /// </summary>
    public SettingsViewModelTests()
    {
        _settings = new SettingsService(Path.Combine(_dir, "settings.json"));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies default settings produce a valid, saveable form.
    /// </summary>
    [Fact]
    public void Constructor_DefaultSettings_HasNoErrorsAndCanSave()
    {
        var vm = CreateViewModel();

        vm.HasErrors.Should().BeFalse();
        vm.SaveCommand.CanExecute(null).Should().BeTrue();
        vm.DailySalaryText.Should().Be("500");
    }

    /// <summary>
    /// Verifies a non-positive salary reports a field error, disables Save and notifies listeners.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("")]
    public void DailySalaryText_Invalid_ReportsFieldErrorAndDisablesSave(string text)
    {
        var vm = CreateViewModel();
        var changed = new List<string?>();
        vm.ErrorsChanged += (_, e) => changed.Add(e.PropertyName);

        vm.DailySalaryText = text;

        vm.GetErrors(nameof(SettingsViewModel.DailySalaryText)).Cast<string>().Should().ContainSingle();
        vm.HasErrors.Should().BeTrue();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
        changed.Should().Contain(nameof(SettingsViewModel.DailySalaryText));
    }

    /// <summary>
    /// Verifies a salary above the cap is rejected.
    /// </summary>
    [Fact]
    public void DailySalaryText_AboveMaximum_ReportsFieldError()
    {
        var vm = CreateViewModel();

        vm.DailySalaryText = (SalarySettings.MaxDailySalary + 1).ToString("0");

        vm.GetErrors(nameof(SettingsViewModel.DailySalaryText)).Cast<string>().Should().ContainSingle();
    }

    /// <summary>
    /// Verifies the reminder-minutes error only applies while the reminder is enabled.
    /// </summary>
    [Fact]
    public void EndOfDayReminderMinutesText_OutOfRange_OnlyErrorsWhenEnabled()
    {
        var vm = CreateViewModel();
        vm.EndOfDayReminderMinutesText = "99";

        vm.GetErrors(nameof(SettingsViewModel.EndOfDayReminderMinutesText)).Cast<string>().Should().BeEmpty();

        vm.EnableEndOfDayReminder = true;

        vm.GetErrors(nameof(SettingsViewModel.EndOfDayReminderMinutesText)).Cast<string>().Should().ContainSingle();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Verifies a milestone larger than the daily salary is rejected when milestones are enabled.
    /// </summary>
    [Fact]
    public void MilestoneAmountText_LargerThanDailySalary_ReportsFieldError()
    {
        var vm = CreateViewModel();
        vm.EnableMilestoneNotifications = true;

        vm.MilestoneAmountText = "501";

        vm.GetErrors(nameof(SettingsViewModel.MilestoneAmountText)).Cast<string>().Should().ContainSingle();
    }

    /// <summary>
    /// Verifies a zero-length schedule blocks saving and shows the schedule error.
    /// </summary>
    [Fact]
    public void WorkEnd_EqualToStart_ShowsErrorMessageAndDisablesSave()
    {
        var vm = CreateViewModel();

        vm.WorkEnd = vm.WorkStart;

        vm.ErrorMessage.Should().NotBeEmpty();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Verifies an overnight shift is a valid schedule.
    /// </summary>
    [Fact]
    public void WorkEnd_BeforeStart_IsValidOvernightShift()
    {
        var vm = CreateViewModel();

        vm.WorkStart = new TimeOnly(22, 0);
        vm.WorkEnd = new TimeOnly(6, 0);

        vm.ErrorMessage.Should().BeEmpty();
        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// Verifies a lunch break outside the work window is rejected.
    /// </summary>
    [Fact]
    public void LunchBreak_OutsideWorkWindow_ShowsErrorMessage()
    {
        var vm = CreateViewModel();

        vm.LunchBreakStart = new TimeOnly(19, 0);
        vm.LunchBreakEnd = new TimeOnly(20, 0);
        vm.LunchBreakEnabled = true;

        vm.ErrorMessage.Should().NotBeEmpty();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Verifies saving persists the edited values, applies them and closes the window.
    /// </summary>
    [Fact]
    public void SaveCommand_ValidForm_PersistsAppliesAndCloses()
    {
        var vm = CreateViewModel();
        vm.DailySalaryText = "1234.567";
        vm.CurrencyText = " $ ";
        vm.IsMiniMode = true;

        vm.SaveCommand.Execute(null);

        var saved = _settings.Load();
        saved.DailySalary.Should().Be(1234.57m);
        saved.Currency.Should().Be("$");
        saved.DisplayMode.Should().Be(DisplayMode.Mini);
        _applier.ReloadCount.Should().Be(1);
        _windows.CloseSettingsCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies a blank currency falls back to the default symbol.
    /// </summary>
    [Fact]
    public void SaveCommand_BlankCurrency_UsesDefaultSymbol()
    {
        var vm = CreateViewModel();
        vm.CurrencyText = "  ";

        vm.SaveCommand.Execute(null);

        _settings.Load().Currency.Should().Be("¥");
    }

    /// <summary>
    /// Verifies the hotkey chosen in the form is persisted unchanged.
    /// </summary>
    [Fact]
    public void SaveCommand_ChangedHotkey_PersistsWin32Values()
    {
        var vm = CreateViewModel();
        vm.HotkeyModifiers = 0x0006;
        vm.HotkeyVirtualKey = 0x74;

        vm.SaveCommand.Execute(null);

        var saved = _settings.Load();
        saved.HotkeyModifiers.Should().Be(0x0006);
        saved.HotkeyVirtualKey.Should().Be(0x74);
        vm.HotkeyDisplayText.Should().Be("Ctrl+Shift+F5");
    }

    /// <summary>
    /// Verifies toggling "run at startup" is forwarded to the platform service.
    /// </summary>
    [Fact]
    public void SaveCommand_RunAtStartupToggled_UpdatesStartupService()
    {
        var vm = CreateViewModel();
        vm.RunAtStartup = true;

        vm.SaveCommand.Execute(null);

        _startup.Enabled.Should().BeTrue();
    }

    /// <summary>
    /// Verifies a failing startup registration keeps the window open and reports the problem.
    /// </summary>
    [Fact]
    public void SaveCommand_StartupToggleFails_KeepsWindowOpenWithError()
    {
        _startup.SetSucceeds = false;
        var vm = CreateViewModel();
        vm.RunAtStartup = true;

        vm.SaveCommand.Execute(null);

        vm.ErrorMessage.Should().NotBeEmpty();
        _windows.CloseSettingsCount.Should().Be(0);
        _applier.ReloadCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies an invalid form is never written to disk.
    /// </summary>
    [Fact]
    public void SaveCommand_InvalidForm_DoesNotPersist()
    {
        var vm = CreateViewModel();
        vm.DailySalaryText = "-5";

        vm.SaveCommand.Execute(null);

        File.Exists(Path.Combine(_dir, "settings.json")).Should().BeFalse();
        _applier.ReloadCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies the chosen language and theme are applied after saving.
    /// </summary>
    [Fact]
    public void SaveCommand_ChangedLanguageAndTheme_AppliesAppearance()
    {
        var vm = CreateViewModel();
        vm.Language = "zh-CN";
        vm.Theme = "light";

        vm.SaveCommand.Execute(null);

        _appearance.Applied.Should().Equal(("zh-CN", "light"));
    }

    /// <summary>
    /// Verifies cancelling closes the window without saving.
    /// </summary>
    [Fact]
    public void CancelCommand_Executed_ClosesWithoutSaving()
    {
        var vm = CreateViewModel();
        vm.DailySalaryText = "999";

        vm.CancelCommand.Execute(null);

        _windows.CloseSettingsCount.Should().Be(1);
        File.Exists(Path.Combine(_dir, "settings.json")).Should().BeFalse();
    }

    /// <summary>
    /// Verifies the opacity is clamped and its percentage text follows.
    /// </summary>
    [Fact]
    public void Opacity_BelowMinimum_ClampsAndUpdatesFormattedText()
    {
        var vm = CreateViewModel();

        vm.Opacity = 0.01;

        vm.Opacity.Should().Be(0.1);
        vm.OpacityFormatted.Should().Be(0.1.ToString("P0"));
    }

    /// <summary>
    /// Verifies the mode radio-button proxies stay mutually exclusive.
    /// </summary>
    [Fact]
    public void IsFlexMode_Set_DeselectsOtherModes()
    {
        var vm = CreateViewModel();

        vm.IsFlexMode = true;

        vm.DisplayMode.Should().Be(DisplayMode.Flex);
        vm.IsNormalMode.Should().BeFalse();
        vm.IsMiniMode.Should().BeFalse();
    }

    /// <summary>
    /// Verifies the hotkey section follows the window service's platform support.
    /// </summary>
    [Fact]
    public void IsHotkeySupported_UnsupportedPlatform_IsFalse()
    {
        _windows.IsHotkeySupported = false;

        CreateViewModel().IsHotkeySupported.Should().BeFalse();
    }

    /// <summary>
    /// Verifies suspend and resume are forwarded to the window service.
    /// </summary>
    [Fact]
    public void SuspendHotkey_ThenResume_ForwardsToWindowService()
    {
        var vm = CreateViewModel();

        vm.SuspendHotkey();
        vm.ResumeHotkey();

        _windows.SuspendCount.Should().Be(1);
        _windows.ResumeCount.Should().Be(1);
    }

    /// <summary>
    /// Creates a view model wired to the test fakes.
    /// </summary>
    private SettingsViewModel CreateViewModel() => new(_settings, _applier, _startup, _windows, _appearance);
}
