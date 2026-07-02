using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.Views;

namespace PayBeat.App.ViewModels;

/// <summary>Represents a language choice shown in the settings language dropdown.</summary>
/// <param name="Code">Language code stored in settings (e.g. <c>"en"</c>, <c>"zh-CN"</c>, <c>"auto"</c>).</param>
/// <param name="Name">Display name shown in the UI.</param>
public record LanguageOption(string Code, string Name);

/// <summary>
/// View model for the settings window. Validates and saves user preferences,
/// then calls <see cref="MainViewModel.ReloadSettings"/> to apply changes immediately.
/// </summary>
public class SettingsViewModel : ViewModelBase
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
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Selected display mode in the settings window. Setting this also raises change notifications
    /// for <see cref="IsNormalMode"/>, <see cref="IsCompactMode"/>, and <see cref="IsMiniMode"/>.
    /// </summary>
    public DisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (SetField(ref _displayMode, value))
            {
                OnPropertyChanged(nameof(IsNormalMode));
                OnPropertyChanged(nameof(IsCompactMode));
                OnPropertyChanged(nameof(IsMiniMode));
                OnPropertyChanged(nameof(IsNoneMode));
                OnPropertyChanged(nameof(IsFlexMode));
            }
        }
    }

    /// <summary>Validation error message shown below the Save button; empty string when there is no error.</summary>
    public string ErrorMessage
    {
        get;
        private set => SetField(ref field, value);
    } = string.Empty;

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

    /// <summary>Proxy property for the Compact radio button; sets <see cref="DisplayMode"/> when assigned <see langword="true"/>.</summary>
    public bool IsCompactMode
    {
        get => _displayMode == DisplayMode.Compact;
        set
        {
            if (value)
            {
                DisplayMode = DisplayMode.Compact;
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
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>Work day start time. Changing this re-evaluates <see cref="SaveCommand"/> availability.</summary>
    public TimeOnly WorkStart
    {
        get => _workStart;
        set
        {
            SetField(ref _workStart, value);
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }

    private bool CanSave() =>
        decimal.TryParse(_dailySalaryText, out var d) && d > 0 && d <= SalarySettings.MaxDailySalary && WorkStart < WorkEnd;

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
        if (!decimal.TryParse(_dailySalaryText, out var salary) || salary <= 0)
        {
            ErrorMessage = LocalizationService.Get("Error.SalaryPositive");
            return;
        }
        if (salary > SalarySettings.MaxDailySalary)
        {
            ErrorMessage = LocalizationService.Get("Error.SalaryTooLarge");
            return;
        }
        if (WorkStart >= WorkEnd)
        {
            ErrorMessage = LocalizationService.Get("Error.WorkEndAfterStart");
            return;
        }

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
            HotkeyVirtualKey = HotkeyVirtualKey
        };

        _service.Save(settings);
        StartupService.SetEnabled(_runAtStartup);
        LocalizationService.Apply(Language);
        _mainVm.ReloadSettings();

        CloseWindow();
    }
}