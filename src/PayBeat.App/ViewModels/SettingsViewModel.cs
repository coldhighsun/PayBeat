using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.Views;
using System.ComponentModel;

namespace PayBeat.App.ViewModels;

/// <summary>Represents a language choice shown in the settings language dropdown.</summary>
/// <param name="Code">Language code stored in settings (e.g. <c>"en"</c>, <c>"zh-CN"</c>, <c>"auto"</c>).</param>
/// <param name="Name">Display name shown in the UI.</param>
public record LanguageOption(string Code, string Name);

/// <summary>
/// View model for the settings window. Validates and saves user preferences,
/// then calls <see cref="MainViewModel.ReloadSettings"/> to apply changes immediately.
/// </summary>
public class SettingsViewModel : ViewModelBase, IDataErrorInfo
{
    private readonly MainViewModel _mainVm;
    private readonly SettingsService _service;
    private bool _alwaysOnTop;
    private string _currencyText;
    private string _dailySalaryText;
    private DisplayMode _displayMode;
    private int _hotkeyModifiers;
    private int _hotkeyVirtualKey;
    private string _language;
    private double _opacity;
    private int _refreshInterval;
    private bool _runAtStartup;
    private TimeOnly _workEnd;
    private TimeOnly _workStart;
    private bool _lunchBreakEnabled;
    private TimeOnly _lunchBreakStart;
    private TimeOnly _lunchBreakEnd;
    private bool _workOnWeekends;
    private bool _enableEndOfDayReminder;
    private string _endOfDayReminderMinutesText;
    private bool _enableMilestoneNotifications;
    private string _milestoneAmountText;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsViewModel"/>, populating fields from the
    /// currently persisted settings.
    /// </summary>
    /// <param name="service">Service used to load and persist settings.</param>
    /// <param name="mainVm">Main view model; <see cref="MainViewModel.ReloadSettings"/> is called after saving.</param>
    public SettingsViewModel(SettingsService service, MainViewModel mainVm)
    {
        _service = service;
        _mainVm = mainVm;

        var s = service.Load();
        _dailySalaryText = s.DailySalary.ToString("G29");
        _currencyText = s.Currency;
        _workStart = s.WorkStart;
        _workEnd = s.WorkEnd;
        _displayMode = s.DisplayMode;
        _alwaysOnTop = s.AlwaysOnTop;
        _opacity = s.Opacity;
        _refreshInterval = s.RefreshInterval;
        _language = s.Language;
        _hotkeyModifiers = s.HotkeyModifiers;
        _hotkeyVirtualKey = s.HotkeyVirtualKey;
        _runAtStartup = StartupService.IsEnabled();
        _lunchBreakEnabled = s.LunchBreakEnabled;
        _lunchBreakStart = s.LunchBreakStart;
        _lunchBreakEnd = s.LunchBreakEnd;
        _workOnWeekends = s.WorkOnWeekends;
        _enableEndOfDayReminder = s.EnableEndOfDayReminder;
        _endOfDayReminderMinutesText = s.EndOfDayReminderMinutes.ToString();
        _enableMilestoneNotifications = s.EnableMilestoneNotifications;
        _milestoneAmountText = s.MilestoneAmount.ToString("G29");

        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(CloseWindow);
    }

    /// <summary>Binds to the Always on Top checkbox in the settings window.</summary>
    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetField(ref _alwaysOnTop, value);
    }

    /// <summary>Fixed list of language options shown in the language dropdown.</summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages
    {
        get;
    } =
        [
        new("auto", "Auto"),
        new("en", "English"),
        new("zh-CN", "中文"),
    ];

    /// <summary>Closes the settings window without saving.</summary>
    public ICommand CancelCommand
    {
        get;
    }

    /// <summary>Raw text entered in the currency symbol field.</summary>
    public string CurrencyText
    {
        get => _currencyText;
        set => SetField(ref _currencyText, value);
    }

    /// <summary>
    /// Raw text entered in the daily salary field. Changing this re-evaluates <see cref="SaveCommand"/> availability.
    /// </summary>
    public string DailySalaryText
    {
        get => _dailySalaryText;
        set
        {
            SetField(ref _dailySalaryText, value);
            Revalidate();
        }
    }

    /// <summary>
    /// Selected display mode in the settings window. Setting this also raises change notifications
    /// for <see cref="IsNormalMode"/> and <see cref="IsMiniMode"/>.
    /// </summary>
    public DisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (SetField(ref _displayMode, value))
            {
                OnPropertyChanged(nameof(IsNormalMode));
                OnPropertyChanged(nameof(IsMiniMode));
                OnPropertyChanged(nameof(IsNoneMode));
                OnPropertyChanged(nameof(IsFlexMode));
            }
        }
    }

    /// <summary>Raw text entered in the end-of-day reminder minutes field; must be an integer in [1, 60].</summary>
    public string EndOfDayReminderMinutesText
    {
        get => _endOfDayReminderMinutesText;
        set
        {
            SetField(ref _endOfDayReminderMinutesText, value);
            Revalidate();
        }
    }

    /// <summary>Whether the milestone earnings tray notification is enabled.</summary>
    public bool EnableMilestoneNotifications
    {
        get => _enableMilestoneNotifications;
        set
        {
            SetField(ref _enableMilestoneNotifications, value);
            Revalidate();
        }
    }

    /// <summary>Whether the end-of-day reminder tray notification is enabled.</summary>
    public bool EnableEndOfDayReminder
    {
        get => _enableEndOfDayReminder;
        set
        {
            SetField(ref _enableEndOfDayReminder, value);
            Revalidate();
        }
    }

    /// <summary>Validation error message shown below the Save button; empty string when there is no error.</summary>
    public string ErrorMessage
    {
        get;
        private set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>Unused; per-field errors are reported via the indexer instead.</summary>
    string IDataErrorInfo.Error => string.Empty;

    /// <summary>Per-field validation error shown as a bubble popup next to the offending input.</summary>
    string IDataErrorInfo.this[string columnName] => columnName switch
    {
        nameof(DailySalaryText) => ValidateDailySalary() ?? string.Empty,
        nameof(EndOfDayReminderMinutesText) => EnableEndOfDayReminder ? ValidateEndOfDayReminderMinutes() ?? string.Empty : string.Empty,
        nameof(MilestoneAmountText) => EnableMilestoneNotifications ? ValidateMilestoneAmount() ?? string.Empty : string.Empty,
        _ => string.Empty,
    };

    /// <summary>Human-readable hotkey string (e.g. <c>Ctrl+Alt+X</c>) shown in the hotkey field.</summary>
    public string HotkeyDisplayText => HotkeyService.Format(HotkeyModifiers, HotkeyVirtualKey);

    /// <summary>
    /// Win32 modifier flags for the hotkey. Setting this raises <see cref="HotkeyDisplayText"/> change.
    /// </summary>
    public int HotkeyModifiers
    {
        get => _hotkeyModifiers;
        set
        {
            if (SetField(ref _hotkeyModifiers, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplayText));
            }
        }
    }

    /// <summary>
    /// Virtual-key code for the hotkey. Setting this raises <see cref="HotkeyDisplayText"/> change.
    /// </summary>
    public int HotkeyVirtualKey
    {
        get => _hotkeyVirtualKey;
        set
        {
            if (SetField(ref _hotkeyVirtualKey, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplayText));
            }
        }
    }

    /// <summary>Proxy property for the Flex radio button; sets <see cref="DisplayMode"/> when assigned <see langword="true"/>.</summary>
    public bool IsFlexMode
    {
        get => _displayMode == DisplayMode.Flex;
        set
        {
            if (value)
            {
                DisplayMode = DisplayMode.Flex;
            }
        }
    }

    /// <summary>Proxy property for the Mini radio button; sets <see cref="DisplayMode"/> when assigned <see langword="true"/>.</summary>
    public bool IsMiniMode
    {
        get => _displayMode == DisplayMode.Mini;
        set
        {
            if (value)
            {
                DisplayMode = DisplayMode.Mini;
            }
        }
    }

    /// <summary>Proxy property for the None radio button; sets <see cref="DisplayMode"/> when assigned <see langword="true"/>.</summary>
    public bool IsNoneMode
    {
        get => _displayMode == DisplayMode.None;
        set
        {
            if (value)
            {
                DisplayMode = DisplayMode.None;
            }
        }
    }

    /// <summary>Proxy property for the Normal radio button; sets <see cref="DisplayMode"/> when assigned <see langword="true"/>.</summary>
    public bool IsNormalMode
    {
        get => _displayMode == DisplayMode.Normal;
        set
        {
            if (value)
            {
                DisplayMode = DisplayMode.Normal;
            }
        }
    }

    /// <summary>Selected language code (e.g. <c>"auto"</c>, <c>"en"</c>, <c>"zh-CN"</c>).</summary>
    public string Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    /// <summary>Whether a lunch break is deducted from earnings.</summary>
    public bool LunchBreakEnabled
    {
        get => _lunchBreakEnabled;
        set
        {
            SetField(ref _lunchBreakEnabled, value);
            Revalidate();
        }
    }

    /// <summary>Lunch break end time. Changing this re-evaluates <see cref="SaveCommand"/> availability.</summary>
    public TimeOnly LunchBreakEnd
    {
        get => _lunchBreakEnd;
        set
        {
            SetField(ref _lunchBreakEnd, value);
            Revalidate();
        }
    }

    /// <summary>Lunch break start time. Changing this re-evaluates <see cref="SaveCommand"/> availability.</summary>
    public TimeOnly LunchBreakStart
    {
        get => _lunchBreakStart;
        set
        {
            SetField(ref _lunchBreakStart, value);
            Revalidate();
        }
    }

    /// <summary>Raw text entered in the milestone amount field.</summary>
    public string MilestoneAmountText
    {
        get => _milestoneAmountText;
        set
        {
            SetField(ref _milestoneAmountText, value);
            Revalidate();
        }
    }

    /// <summary>Widget opacity at idle, clamped to [0.1, 1.0].</summary>
    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, Math.Clamp(value, 0.1, 1.0));
    }

    /// <summary>Earnings refresh interval in seconds, clamped to [1, 60].</summary>
    public int RefreshInterval
    {
        get => _refreshInterval;
        set => SetField(ref _refreshInterval, Math.Clamp(value, 1, 60));
    }

    /// <summary>Whether the app is registered to launch at Windows startup.</summary>
    public bool RunAtStartup
    {
        get => _runAtStartup;
        set => SetField(ref _runAtStartup, value);
    }

    /// <summary>Validates input and persists all settings. Disabled when validation fails.</summary>
    public ICommand SaveCommand
    {
        get;
    }

    /// <summary>Work day end time. Changing this re-evaluates <see cref="SaveCommand"/> availability.</summary>
    public TimeOnly WorkEnd
    {
        get => _workEnd;
        set
        {
            SetField(ref _workEnd, value);
            Revalidate();
        }
    }

    /// <summary>Work day start time. Changing this re-evaluates <see cref="SaveCommand"/> availability.</summary>
    public TimeOnly WorkStart
    {
        get => _workStart;
        set
        {
            SetField(ref _workStart, value);
            Revalidate();
        }
    }

    /// <summary>Whether earnings accrue on Saturdays and Sundays.</summary>
    public bool WorkOnWeekends
    {
        get => _workOnWeekends;
        set => SetField(ref _workOnWeekends, value);
    }

    private bool CanSave() => Validate() is null;

    /// <summary>
    /// Updates the bottom-of-window error message and Save availability. Only schedule-related errors are
    /// shown here — the daily salary, milestone amount, and reminder minutes fields report their own errors
    /// via a bubble popup next to the field instead.
    /// </summary>
    private void Revalidate()
    {
        ErrorMessage = ValidateSchedule() ?? string.Empty;
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
    }

    /// <summary>Validates work hours and lunch break; returns <see langword="null"/> when valid.</summary>
    private string? ValidateSchedule()
    {
        if (WorkStart >= WorkEnd)
        {
            return LocalizationService.Get("Error.WorkEndAfterStart");
        }
        if (LunchBreakEnabled && (LunchBreakStart >= LunchBreakEnd || LunchBreakStart < WorkStart || LunchBreakEnd > WorkEnd))
        {
            return LocalizationService.Get("Error.LunchBreakInvalid");
        }

        return null;
    }

    /// <summary>Validates all fields and returns the first error message, or <see langword="null"/> when everything is valid.</summary>
    private string? Validate()
    {
        var salaryError = ValidateDailySalary();
        if (salaryError is not null)
        {
            return salaryError;
        }
        var scheduleError = ValidateSchedule();
        if (scheduleError is not null)
        {
            return scheduleError;
        }
        if (EnableEndOfDayReminder)
        {
            var minutesError = ValidateEndOfDayReminderMinutes();
            if (minutesError is not null)
            {
                return minutesError;
            }
        }
        if (EnableMilestoneNotifications)
        {
            var milestoneError = ValidateMilestoneAmount();
            if (milestoneError is not null)
            {
                return milestoneError;
            }
        }

        return null;
    }

    /// <summary>Validates <see cref="DailySalaryText"/>; returns <see langword="null"/> when valid.</summary>
    private string? ValidateDailySalary()
    {
        if (!decimal.TryParse(_dailySalaryText, out var salary) || salary <= 0)
        {
            return LocalizationService.Get("Error.SalaryPositive");
        }
        if (salary > SalarySettings.MaxDailySalary)
        {
            return LocalizationService.Get("Error.SalaryTooLarge");
        }

        return null;
    }

    /// <summary>Validates <see cref="EndOfDayReminderMinutesText"/>; returns <see langword="null"/> when valid.</summary>
    private string? ValidateEndOfDayReminderMinutes()
    {
        if (!int.TryParse(_endOfDayReminderMinutesText, out var minutes) || minutes < 1 || minutes > 60)
        {
            return LocalizationService.Get("Error.EndOfDayReminderMinutesInvalid");
        }

        return null;
    }

    /// <summary>Validates <see cref="MilestoneAmountText"/>; returns <see langword="null"/> when valid.</summary>
    private string? ValidateMilestoneAmount()
    {
        if (!decimal.TryParse(_milestoneAmountText, out var milestone) || milestone <= 0)
        {
            return LocalizationService.Get("Error.MilestoneAmountPositive");
        }
        if (decimal.TryParse(_dailySalaryText, out var daily) && milestone > daily)
        {
            return LocalizationService.Get("Error.MilestoneAmountTooLarge");
        }

        return null;
    }

    private void CloseWindow()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w is SettingsWindow)
            {
                w.Close();
                break;
            }
        }
    }

    private void Save()
    {
        if (Validate() is not null)
        {
            ErrorMessage = ValidateSchedule() ?? string.Empty;
            return;
        }

        var salary = decimal.Parse(_dailySalaryText);
        var existing = _service.Load();
        var settings = existing with
        {
            DailySalary = Math.Round(salary, 2),
            WorkStart = WorkStart,
            WorkEnd = WorkEnd,
            Currency = string.IsNullOrWhiteSpace(_currencyText) ? "¥" : _currencyText.Trim(),
            DisplayMode = DisplayMode,
            AlwaysOnTop = AlwaysOnTop,
            Opacity = Opacity,
            RefreshInterval = RefreshInterval,
            Language = Language,
            HotkeyModifiers = HotkeyModifiers,
            HotkeyVirtualKey = HotkeyVirtualKey,
            LunchBreakEnabled = LunchBreakEnabled,
            LunchBreakStart = LunchBreakStart,
            LunchBreakEnd = LunchBreakEnd,
            WorkOnWeekends = WorkOnWeekends,
            EnableEndOfDayReminder = EnableEndOfDayReminder,
            EndOfDayReminderMinutes = EnableEndOfDayReminder && int.TryParse(_endOfDayReminderMinutesText, out var parsedMinutes)
                ? parsedMinutes
                : existing.EndOfDayReminderMinutes,
            EnableMilestoneNotifications = EnableMilestoneNotifications,
            MilestoneAmount = EnableMilestoneNotifications && decimal.TryParse(_milestoneAmountText, out var parsedMilestone)
                ? Math.Round(parsedMilestone, 2)
                : existing.MilestoneAmount
        };

        _service.Save(settings);
        StartupService.SetEnabled(_runAtStartup);
        LocalizationService.Apply(Language);
        _mainVm.ReloadSettings();

        CloseWindow();
    }
}