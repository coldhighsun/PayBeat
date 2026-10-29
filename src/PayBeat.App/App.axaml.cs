using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Platform;
using PayBeat.App.Services;
using PayBeat.App.ViewModels;
using PayBeat.App.Views;

namespace PayBeat.App;

/// <summary>
/// Application entry point. Owns the top-level object graph: <see cref="SettingsService"/>,
/// <see cref="MainViewModel"/>, <see cref="MainWindow"/>, and the platform hotkey service.
/// Saves window position to settings on exit and hides/shows all windows from the global hotkey.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Name of the machine-wide mutex that enforces a single running instance; the Windows installer checks the same name.
    /// </summary>
    private const string SingleInstanceName = "PayBeat_SingleInstance";

    /// <summary>
    /// Windows hidden by the global hotkey, restored when it is pressed again.
    /// </summary>
    private readonly List<Window> _hiddenWindows = [];

    /// <summary>
    /// The desktop lifetime that owns the windows.
    /// </summary>
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    /// <summary>
    /// Platform global-hotkey service.
    /// </summary>
    private IHotkeyService? _hotkeyService;

    /// <summary>
    /// The widget's view model.
    /// </summary>
    private MainViewModel? _mainVm;

    /// <summary>
    /// The widget window.
    /// </summary>
    private MainWindow? _mainWindow;

    /// <summary>
    /// Creates and tracks the settings and about windows.
    /// </summary>
    private AppWindowService? _windowService;

    /// <summary>
    /// Persists user settings.
    /// </summary>
    private SettingsService? _settingsService;

    /// <summary>
    /// Mutex held for the lifetime of the process to detect a second instance.
    /// </summary>
    private Mutex? _singleInstanceMutex;

    /// <summary>
    /// Whether this process created (and therefore owns) the single-instance mutex.
    /// </summary>
    private bool _ownsSingleInstanceMutex;

    /// <summary>
    /// Settings loaded at startup, used to restore the window placement once it is first rendered.
    /// </summary>
    private SalarySettings? _startupSettings;

    /// <summary>
    /// Shows in-app toast notifications.
    /// </summary>
    private ToastService? _toastService;

    /// <summary>
    /// Owns the tray icon and its menu.
    /// </summary>
    private TrayIconService? _trayIconService;

    /// <summary>
    /// Whether all windows are currently hidden via the global hotkey.
    /// </summary>
    private bool _windowsHidden;

    /// <summary>
    /// Whether the window position was already persisted during this shutdown.
    /// </summary>
    private bool _positionSaved;

    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Start(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Builds the object graph and shows the widget (or only the tray icon, in <see cref="DisplayMode.None"/>).
    /// </summary>
    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var settings = LoadStartupSettings();

        if (!TryAcquireSingleInstance(desktop))
        {
            return;
        }

        desktop.ShutdownRequested += (_, _) => SavePositionOnce(captureNow: true);
        desktop.Exit += (_, _) => OnExit();

        CreateMainViewModelAndWindow(desktop);

        if (settings.DisplayMode == DisplayMode.None)
        {
            _mainWindow!.Opened -= OnMainWindowOpened;
        }
        else
        {
            ShowMainWindow(settings);
        }

        _trayIconService = new TrayIconService(_mainVm!, ActivateMainWindow);
        _toastService = new ToastService(_mainVm!, ExternalLauncher.Open);
        CheckForUpdatesOnStartup();
    }

    /// <summary>
    /// Releases resources and persists the final window position.
    /// </summary>
    private void OnExit()
    {
        SavePositionOnce(captureNow: false);

        _toastService?.Dispose();
        _trayIconService?.Dispose();
        _hotkeyService?.Dispose();
        _mainVm?.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
    }

    /// <summary>
    /// Persists the widget position once per shutdown. While the window is still open the live position
    /// is captured; afterwards the snapshot taken in the window's Closing handler is used.
    /// </summary>
    private void SavePositionOnce(bool captureNow)
    {
        if (_positionSaved || _mainWindow is null)
        {
            return;
        }

        var pos = captureNow ? _mainWindow.CapturePosition() : _mainWindow.LastKnownPosition;
        if (pos is null)
        {
            return;
        }

        _positionSaved = true;
        SaveWindowPosition(pos);
    }

    /// <summary>
    /// Restores the saved placement once the window is first shown, because clamping depends on its measured size.
    /// </summary>
    private static void ApplyStartupPlacement(MainWindow mainWindow, SalarySettings settings)
    {
        if (settings.DisplayMode == DisplayMode.Flex)
        {
            var flexScreen = settings.FlexPosition is { } flex
                ? ScreenHelper.FindScreenForRestore(mainWindow, new PixelPoint((int)flex.Left, (int)flex.Top), flex.ScreenDeviceName)
                : null;
            mainWindow.ApplyFlexBounds(flexScreen);
            return;
        }

        if (GetSavedPosition(settings, settings.DisplayMode) is not { } pos)
        {
            return;
        }

        var point = new PixelPoint((int)pos.Left, (int)pos.Top);
        var screen = ScreenHelper.FindScreenForRestore(mainWindow, point, pos.ScreenDeviceName);
        mainWindow.Position = point;
        if (screen is not null)
        {
            mainWindow.ClampToWorkArea(screen.Bounds);
        }
    }

    /// <summary>
    /// Returns the persisted position for <paramref name="mode"/>, or <see langword="null"/> when the mode has none.
    /// </summary>
    private static WindowPosition? GetSavedPosition(SalarySettings settings, DisplayMode mode) =>
        mode switch
        {
            DisplayMode.Normal => settings.NormalPosition,
            DisplayMode.Mini => settings.MiniPosition,
            DisplayMode.None => null,
            DisplayMode.Flex => null,
            _ => null
        };

    /// <summary>
    /// Brings the widget to the front with an attention animation (tray icon click).
    /// </summary>
    private void ActivateMainWindow()
    {
        if (_mainWindow == null || _mainVm == null || _mainVm.DisplayMode == DisplayMode.None)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.PlayAttentionAnimation();
    }

    /// <summary>
    /// Fires a best-effort, fire-and-forget update check, throttled to once per 24 hours via
    /// <see cref="SettingsLastCheckStore"/>.
    /// </summary>
    private void CheckForUpdatesOnStartup()
    {
        _ = Task.Run(async () =>
        {
            var info = await new UpdateCheckService(_settingsService!).GetLatestReleaseAsync();
            if (info != null)
            {
                Dispatcher.UIThread.Post(() => _mainVm!.NotifyUpdateAvailable(info.Version, info.HtmlUrl));
            }
        });
    }

    /// <summary>
    /// Creates the services, view model and window, and registers the global hotkey.
    /// </summary>
    private void CreateMainViewModelAndWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _hotkeyService = PlatformServices.CreateHotkeyService();
        _windowService = new AppWindowService(_settingsService!, PlatformServices.CreateStartupService(), _hotkeyService, desktop);

        _mainVm = new MainViewModel(_settingsService!, _windowService);
        _mainVm.HotkeySettingsChanged += OnHotkeySettingsChanged;

        _mainWindow = new MainWindow(PlatformServices.CreateTopmostEnforcer()) { DataContext = _mainVm };
        _mainWindow.Opened += OnMainWindowOpened;

        _windowService.SettingsApplier = _mainVm;
        _windowService.MainWindow = _mainWindow;

        // The native handle exists once the window object is created, even if it is never shown (tray-only start).
        RegisterHotkey();
    }

    /// <summary>
    /// Loads settings and applies the configured language and theme.
    /// </summary>
    private SalarySettings LoadStartupSettings()
    {
        _settingsService = new SettingsService();
        var settings = _settingsService.Load();
        _startupSettings = settings;
        new AppearanceService().Apply(settings.Language, settings.Theme);
        return settings;
    }

    /// <summary>
    /// Re-registers the hotkey after settings changed and reports a conflict.
    /// </summary>
    private void OnHotkeySettingsChanged()
    {
        var s = _settingsService!.Load();
        if (_hotkeyService != null && !_hotkeyService.Update(s.HotkeyModifiers, s.HotkeyVirtualKey))
        {
            ShowHotkeyConflict(s);
        }
    }

    /// <summary>
    /// Applies the saved placement after the window opened.
    /// </summary>
    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        if (_mainWindow == null || _startupSettings == null)
        {
            return;
        }

        _mainWindow.Opened -= OnMainWindowOpened;

        // The window is only measured after the first layout pass, which runs after Opened.
        Dispatcher.UIThread.Post(
            () =>
            {
                ApplyStartupPlacement(_mainWindow, _startupSettings);
                _mainWindow.IsRestoringStartupPosition = false;
            },
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Registers the global hotkey and hooks its trigger.
    /// </summary>
    private void RegisterHotkey()
    {
        var s = _settingsService!.Load();
        var registered = _hotkeyService!.Register(_mainWindow!, s.HotkeyModifiers, s.HotkeyVirtualKey);
        if (!registered)
        {
            ShowHotkeyConflict(s);
        }

        _hotkeyService.Triggered += ToggleWindowVisibility;
    }

    /// <summary>
    /// Tells the user the configured hotkey is already taken by another application.
    /// </summary>
    private void ShowHotkeyConflict(SalarySettings s)
    {
        var key = HotkeyKeyMap.Format(s.HotkeyModifiers, s.HotkeyVirtualKey);
        new MessageWindow("PayBeat", string.Format(LocalizationService.Get("Error.HotkeyConflict"), key)).Show();
    }

    /// <summary>Merges <paramref name="pos"/> into the settings slot for the active display mode and persists it.</summary>
    private void SaveWindowPosition(WindowPosition pos)
    {
        if (_mainVm == null || _settingsService == null)
        {
            return;
        }

        var settings = _settingsService.Load();
        var updated = _mainVm.DisplayMode switch
        {
            DisplayMode.Normal => settings with { NormalPosition = pos },
            DisplayMode.Mini => settings with { MiniPosition = pos },
            DisplayMode.None => settings,
            DisplayMode.Flex => settings with { FlexPosition = pos },
            _ => settings
        };
        _settingsService.Save(updated);
    }

    /// <summary>
    /// Shows the widget, positioned at its saved location for the Normal and Mini modes.
    /// </summary>
    private void ShowMainWindow(SalarySettings settings)
    {
        _mainWindow!.IsRestoringStartupPosition = settings.DisplayMode is DisplayMode.Normal or DisplayMode.Mini or DisplayMode.Flex;
        if (settings.DisplayMode is DisplayMode.Normal or DisplayMode.Mini)
        {
            var startupPos = GetSavedPosition(settings, settings.DisplayMode);
            if (startupPos != null)
            {
                _mainWindow.RestorePosition(new PixelPoint((int)startupPos.Left, (int)startupPos.Top));
            }
        }

        _mainWindow.Show();
    }

    /// <summary>
    /// Hides every visible window (except toasts), or shows the ones hidden earlier.
    /// </summary>
    private void ToggleWindowVisibility()
    {
        if (_windowsHidden)
        {
            foreach (var w in _hiddenWindows)
            {
                w.Show();
            }

            _hiddenWindows.Clear();
            _windowsHidden = false;
            _mainVm?.ResumeNotifications();
            _trayIconService?.SetHidden(false);
        }
        else
        {
            foreach (var w in _desktop!.Windows.Where(w => w.IsVisible && w is not ToastWindow).ToList())
            {
                _hiddenWindows.Add(w);
                w.Hide();
            }

            _toastService?.DismissAll();
            _windowsHidden = true;
            _mainVm?.SuspendNotifications();
            _trayIconService?.SetHidden(true);
        }
    }

    /// <summary>
    /// Acquires the single-instance mutex; if another instance is running, tells the user and shuts down.
    /// </summary>
    private bool TryAcquireSingleInstance(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceName, out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (createdNew)
        {
            return true;
        }

        var message = new MessageWindow("PayBeat", LocalizationService.Get("Error.AlreadyRunning"));
        message.Closed += (_, _) => desktop.Shutdown();
        message.Show();
        return false;
    }
}
