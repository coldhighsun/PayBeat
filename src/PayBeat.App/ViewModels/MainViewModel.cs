using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.Views;

namespace PayBeat.App.ViewModels;

/// <summary>
/// Primary view model for the floating widget. Owns the refresh timer, drives earned/progress
/// state, and exposes commands for display mode switching and settings.
/// </summary>
public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private decimal _earned;
    private double _progress;
    private SalarySettings _settings;
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

        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenAboutCommand = new RelayCommand(OpenAbout);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        SetNormalModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.Normal));
        SetCompactModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.Compact));
        SetMiniModeCommand = new RelayCommand(() => SetDisplayMode(DisplayMode.Mini));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.RefreshInterval) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    /// <summary>Raised when hotkey settings change so <c>App</c> can re-register the global hotkey.</summary>
    public event Action? HotkeySettingsChanged;

    /// <summary>Whether the window should stay above all other windows.</summary>
    public bool AlwaysOnTop => _settings.AlwaysOnTop;

    /// <summary>Currency symbol shown before the earnings amount.</summary>
    public string Currency => _settings.Currency;

    /// <summary>Configured daily salary used only for display purposes in the settings summary.</summary>
    public decimal DailySalary => _settings.DailySalary;

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
    public string EarnedFormatted =>
        $"{_settings.Currency}{Earned:N2}";

    /// <summary>Shuts down the application.</summary>
    public ICommand ExitCommand
    {
        get;
    }

    /// <summary>Convenience flag bound to the display mode menu checkboxes.</summary>
    public bool IsCompactMode => _settings.DisplayMode == DisplayMode.Compact;

    /// <summary>Convenience flag bound to the display mode menu checkboxes.</summary>
    public bool IsMiniMode => _settings.DisplayMode == DisplayMode.Mini;

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
        private set => SetField(ref _progress, value);
    }

    /// <summary>Switches the widget to <see cref="DisplayMode.Compact"/> and saves the change.</summary>
    public ICommand SetCompactModeCommand
    {
        get;
    }

    /// <summary>Switches the widget to <see cref="DisplayMode.Mini"/> and saves the change.</summary>
    public ICommand SetMiniModeCommand
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

    /// <summary>Work day start time, exposed for display in the settings summary.</summary>
    public TimeOnly WorkStart => _settings.WorkStart;

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
        OnPropertyChanged(nameof(WorkEnd));
        OnPropertyChanged(nameof(DailySalary));
        OnPropertyChanged(nameof(Currency));
        OnPropertyChanged(nameof(DisplayMode));
        OnPropertyChanged(nameof(AlwaysOnTop));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(IsNormalMode));
        OnPropertyChanged(nameof(IsCompactMode));
        OnPropertyChanged(nameof(IsMiniMode));
        _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshInterval);
        _wakeTimer?.Stop();
        _wakeTimer = null;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
        HotkeySettingsChanged?.Invoke();
        Refresh();
    }

    private void OpenAbout()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w is AboutWindow existing)
            {
                existing.Activate();
                return;
            }
        }

        new AboutWindow().Show();
    }

    private void OpenSettings()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w is SettingsWindow existing)
            {
                existing.Activate();
                return;
            }
        }

        var win = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_settingsService, this)
        };
        win.Show();
    }

    private void Refresh()
    {
        var now = DateTime.Now;
        Earned = EarningsCalculator.Calculate(_settings, now);
        Progress = EarningsCalculator.WorkdayProgress(_settings, now);

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
        _wakeTimer = new DispatcherTimer { Interval = delay };
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