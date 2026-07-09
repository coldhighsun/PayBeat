using Microsoft.UI.Xaml;
using PayBeat.WinUI.Services;
using PayBeat.Core.Helpers;
using PayBeat.Core.Models;
using PayBeat.Core.Services;
using PayBeat.Core.ViewModels;

namespace PayBeat.WinUI.ViewModels;

/// <summary>
/// Primary view model for the floating widget. Owns the refresh timer, drives earned/progress
/// state, and exposes commands for display mode switching and settings. <c>MainWindow</c> reacts
/// to <see cref="DisplayMode"/> property-change notifications to drive its own AppWindow chrome,
/// mirroring the WPF build's <c>OnVmPropertyChanged</c> pattern - this view model has no knowledge
/// of window mechanics.
/// </summary>
public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private Views.AboutWindow? _aboutWindow;
    private decimal _earned;
    private TimeSpan _elapsed;
    private bool _endOfDayReminderSent;
    private decimal _nextMilestoneThreshold;
    private bool _notificationsSuspended;
    private DateOnly _notifiedDate;
    private double _progress;
    private TimeSpan _remaining;
    private SalarySettings _settings;
    private Views.SettingsWindow? _settingsWindow;
    private DispatcherTimer? _wakeTimer;

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>, loads settings, starts the refresh timer,
    /// and performs an immediate <see cref="Refresh"/> to populate the initial display.
    /// </summary>
    /// <param name="settingsService">Service used to load and save salary settings.</param>
    public MainViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = _settingsService.Load();
        _notifiedDate = DateOnly.FromDateTime(DateTime.Now);
        _nextMilestoneThreshold = _settings.MilestoneAmount;

        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenAboutCommand = new RelayCommand(OpenAbout);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());
        SetNormalModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.Normal));
        SetMiniModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.Mini));
        SetNoneModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.None));
        SetFlexModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.Flex));

        _timer = new()
        {
            Interval = TimeSpan.FromSeconds(_settings.RefreshInterval)
        };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    /// <summary>Raised when the Exit command is invoked; <c>App</c> owns actual process shutdown.</summary>
    public event Action? ExitRequested;

    /// <summary>Raised when hotkey settings change so <c>App</c> can re-register the global hotkey.</summary>
    public event Action? HotkeySettingsChanged;

    /// <summary>Raised when a milestone or end-of-day reminder should be shown as a tray notification.</summary>
    public event Action<string, string>? NotificationRequested;

    /// <summary>Whether the window should stay above all other windows.</summary>
    public bool AlwaysOnTop => _settings.AlwaysOnTop;

    /// <summary>Currency symbol shown before the earnings amount.</summary>
    public string Currency => _settings.Currency;

    /// <summary>Configured daily salary used only for display purposes in the settings summary.</summary>
    public decimal DailySalary => _settings.DailySalary;

    /// <summary>
    /// <see cref="DailySalary"/> formatted as <c>N2</c>. WinUI3's classic <c>{Binding}</c> has no
    /// <c>StringFormat</c> (unlike WPF), so views bind to this instead of formatting inline.
    /// </summary>
    public string DailySalaryFormatted => DailySalary.ToString("N2");

    /// <summary>Currently active display mode; drives the view template selection in <c>MainWindow</c>.</summary>
    public DisplayMode DisplayMode => _settings.DisplayMode;

    /// <summary>Amount earned so far today. Setting this also notifies <see cref="EarnedFormatted"/>.</summary>
    public decimal Earned
    {
        get => _earned;
        private set
        {
            SetField(ref _earned, value);
            OnPropertyChanged(nameof(EarnedFormatted));
        }
    }

    /// <summary>Earned amount formatted as <c>{Currency}{Amount:N2}</c> (e.g. <c>¥123.45</c>).</summary>
    public string EarnedFormatted => $"{_settings.Currency}{Earned:N2}";

    /// <summary>Time elapsed since work start, clamped to the workday window. Setting this also notifies <see cref="ElapsedFormatted"/>.</summary>
    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set
        {
            SetField(ref _elapsed, value);
            OnPropertyChanged(nameof(ElapsedFormatted));
        }
    }

    /// <summary>Elapsed work time formatted as <c>hh:mm:ss</c>.</summary>
    public string ElapsedFormatted => Elapsed.ToString(@"hh\:mm\:ss");

    /// <summary>Shuts down the application.</summary>
    public ICommand ExitCommand
    {
        get;
    }

    /// <summary>Convenience flag bound to the display mode menu checkboxes.</summary>
    public bool IsFlexMode => _settings.DisplayMode == DisplayMode.Flex;

    /// <summary>Convenience flag bound to the display mode menu checkboxes.</summary>
    public bool IsMiniMode => _settings.DisplayMode == DisplayMode.Mini;

    /// <summary>Convenience flag bound to the display mode menu checkboxes.</summary>
    public bool IsNoneMode => _settings.DisplayMode == DisplayMode.None;

    /// <summary>Convenience flag bound to the display mode menu checkboxes.</summary>
    public bool IsNormalMode => _settings.DisplayMode == DisplayMode.Normal;

    /// <summary>Window opacity at idle (not hovered).</summary>
    public double Opacity => _settings.Opacity;

    /// <summary>Opens the about window, or activates it if already open.</summary>
    public ICommand OpenAboutCommand
    {
        get;
    }

    /// <summary>Opens the settings window, or activates it if already open.</summary>
    public ICommand OpenSettingsCommand
    {
        get;
    }

    /// <summary>Workday completion fraction in [0.0, 1.0], bound to the progress bar.</summary>
    public double Progress
    {
        get => _progress;
        private set
        {
            SetField(ref _progress, value);
            OnPropertyChanged(nameof(ProgressFormatted));
        }
    }

    /// <summary><see cref="Progress"/> formatted as a percentage, e.g. <c>42.00%</c>.</summary>
    public string ProgressFormatted => Progress.ToString("P2");

    /// <summary>Time remaining until work end, clamped to the workday window. Setting this also notifies <see cref="RemainingFormatted"/>.</summary>
    public TimeSpan Remaining
    {
        get => _remaining;
        private set
        {
            SetField(ref _remaining, value);
            OnPropertyChanged(nameof(RemainingFormatted));
        }
    }

    /// <summary>Remaining work time formatted as <c>hh:mm:ss</c>.</summary>
    public string RemainingFormatted => Remaining.ToString(@"hh\:mm\:ss");

    /// <summary>Switches the widget to <see cref="DisplayMode.Flex"/> and saves the change.</summary>
    public ICommand SetFlexModeCommand
    {
        get;
    }

    /// <summary>Switches the widget to <see cref="DisplayMode.Mini"/> and saves the change.</summary>
    public ICommand SetMiniModeCommand
    {
        get;
    }

    /// <summary>Switches the widget to <see cref="DisplayMode.None"/> and saves the change.</summary>
    public ICommand SetNoneModeCommand
    {
        get;
    }

    /// <summary>Switches the widget to <see cref="DisplayMode.Normal"/> and saves the change.</summary>
    public ICommand SetNormalModeCommand
    {
        get;
    }

    /// <summary>Work day end time, exposed for display in the settings summary.</summary>
    public TimeOnly WorkEnd => _settings.WorkEnd;

    /// <summary><see cref="WorkEnd"/> formatted as <c>HH:mm</c>; see <see cref="DailySalaryFormatted"/> for why.</summary>
    public string WorkEndFormatted => WorkEnd.ToString("HH:mm");

    /// <summary>Work day start time, exposed for display in the settings summary.</summary>
    public TimeOnly WorkStart => _settings.WorkStart;

    /// <summary><see cref="WorkStart"/> formatted as <c>HH:mm</c>; see <see cref="DailySalaryFormatted"/> for why.</summary>
    public string WorkStartFormatted => WorkStart.ToString("HH:mm");

    /// <inheritdoc/>
    public void Dispose()
    {
        _wakeTimer?.Stop();
        _timer.Stop();
    }

    /// <summary>
    /// Reloads settings from disk and notifies all bound properties. Called by
    /// <see cref="SettingsViewModel"/> after the user saves changes.
    /// Also raises <see cref="HotkeySettingsChanged"/> so <c>App</c> can re-register the hotkey.
    /// </summary>
    public void ReloadSettings()
    {
        _settings = _settingsService.Load();
        OnPropertyChanged(nameof(WorkStart));
        OnPropertyChanged(nameof(WorkStartFormatted));
        OnPropertyChanged(nameof(WorkEnd));
        OnPropertyChanged(nameof(WorkEndFormatted));
        OnPropertyChanged(nameof(DailySalary));
        OnPropertyChanged(nameof(DailySalaryFormatted));
        OnPropertyChanged(nameof(Currency));
        OnPropertyChanged(nameof(DisplayMode));
        OnPropertyChanged(nameof(AlwaysOnTop));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(IsNormalMode));
        OnPropertyChanged(nameof(IsMiniMode));
        OnPropertyChanged(nameof(IsNoneMode));
        OnPropertyChanged(nameof(IsFlexMode));
        _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshInterval);
        _wakeTimer?.Stop();
        _wakeTimer = null;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
        _nextMilestoneThreshold = _settings.MilestoneAmount;
        _endOfDayReminderSent = false;
        HotkeySettingsChanged?.Invoke();
        Refresh();
    }

    /// <summary>Resumes end-of-day/milestone tray notifications after a prior <see cref="SuspendNotifications"/> call.</summary>
    public void ResumeNotifications() => _notificationsSuspended = false;

    /// <summary>Suppresses end-of-day/milestone tray notifications while the widget is hidden.</summary>
    public void SuspendNotifications() => _notificationsSuspended = true;

    private void CheckNotifications(DateTime now)
    {
        if (_notificationsSuspended || !EarningsCalculator.IsWorkday(_settings, now))
        {
            return;
        }

        if (_settings.EnableMilestoneNotifications && _settings.MilestoneAmount > 0)
        {
            while (Earned >= _nextMilestoneThreshold)
            {
                NotificationRequested?.Invoke(
                    LocalizationService.Instance["Notification.MilestoneTitle"],
                    string.Format(LocalizationService.Instance["Notification.MilestoneBody"], $"{_settings.Currency}{_nextMilestoneThreshold:N2}"));
                _nextMilestoneThreshold += _settings.MilestoneAmount;
            }
        }

        if (_settings.EnableEndOfDayReminder && !_endOfDayReminderSent
            && Remaining > TimeSpan.Zero && Remaining <= TimeSpan.FromMinutes(_settings.EndOfDayReminderMinutes))
        {
            _endOfDayReminderSent = true;
            NotificationRequested?.Invoke(
                LocalizationService.Instance["Notification.EndOfDayTitle"],
                string.Format(LocalizationService.Instance["Notification.EndOfDayBody"], _settings.EndOfDayReminderMinutes));
        }
    }

    private void OpenAbout()
    {
        if (_aboutWindow != null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Activate();
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new(new(_settingsService, this));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void Refresh()
    {
        var now = DateTime.Now;

        var today = DateOnly.FromDateTime(now);
        if (today != _notifiedDate)
        {
            _notifiedDate = today;
            _nextMilestoneThreshold = _settings.MilestoneAmount;
            _endOfDayReminderSent = false;
        }

        Earned = EarningsCalculator.Calculate(_settings, now);
        Progress = EarningsCalculator.WorkdayProgress(_settings, now);
        Elapsed = EarningsCalculator.Elapsed(_settings, now);
        Remaining = EarningsCalculator.Remaining(_settings, now);

        CheckNotifications(now);

        var current = TimeOnly.FromDateTime(now);
        if (current <= _settings.WorkStart || current >= _settings.WorkEnd)
        {
            _timer.Stop();
            ScheduleWakeTimer(now);
        }
    }

    private void ScheduleWakeTimer(DateTime now)
    {
        _wakeTimer?.Stop();

        var current = TimeOnly.FromDateTime(now);
        var nextStart = current < _settings.WorkStart
            ? now.Date + _settings.WorkStart.ToTimeSpan()
            : now.Date.AddDays(1) + _settings.WorkStart.ToTimeSpan();

        var delay = nextStart - now;
        _wakeTimer = new()
        {
            Interval = delay
        };
        _wakeTimer.Tick += (_, _) =>
        {
            _wakeTimer!.Stop();
            _wakeTimer = null;
            _timer.Start();
            Refresh();
        };
        _wakeTimer.Start();
    }

    private void SetDisplayMode(DisplayMode mode)
    {
        var current = _settingsService.Load();
        if (current.DisplayMode == mode)
        {
            return;
        }

        _settingsService.Save(current with
        {
            DisplayMode = mode
        });
        ReloadSettings();
    }
}