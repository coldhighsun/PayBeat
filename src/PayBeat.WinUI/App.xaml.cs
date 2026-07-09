using Microsoft.UI.Xaml;
using PayBeat.Core.Models;
using PayBeat.Core.Services;
using PayBeat.WinUI.Helpers;
using PayBeat.WinUI.Services;
using PayBeat.WinUI.ViewModels;

namespace PayBeat.WinUI;

/// <summary>
/// Application entry point. Wires settings load/save, startup position restore, the global
/// hotkey, the tray icon, and the main view model.
/// </summary>
public partial class App
{
    private HotkeyService? _hotkeyService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;
    private SettingsService? _settingsService;
    private Mutex? _singleInstanceMutex;
    private TrayIconService? _trayIconService;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceMutex = new(initiallyOwned: true, "PayBeat_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Environment.Exit(0);
            return;
        }

        _settingsService = new();
        var settings = _settingsService.Load();
        LocalizationService.Instance.Apply(settings.Language);

        _mainViewModel = new(_settingsService);
        _mainViewModel.HotkeySettingsChanged += OnHotkeySettingsChanged;
        _mainViewModel.NotificationRequested += OnNotificationRequested;
        _mainViewModel.ExitRequested += OnExitRequested;

        _mainWindow = new(_mainViewModel);
        RestoreStartupPosition(_mainWindow, settings);
        _mainWindow.Activate();

        _hotkeyService = new();
        _hotkeyService.Register(_mainWindow.Handle, settings.HotkeyModifiers, settings.HotkeyVirtualKey);
        _hotkeyService.Triggered += OnHotkeyTriggered;

        _trayIconService = new(_mainWindow, () => _mainWindow?.ActivateWithAttention());
        _trayIconService.ExitRequested += OnExitRequested;
    }

    /// <summary>
    /// Restores the saved position for the settings' active display mode, translating from the
    /// legacy WPF build's 96-DPI logical units into the physical pixels <c>AppWindow</c> expects.
    /// </summary>
    private static void RestoreStartupPosition(MainWindow window, SalarySettings settings)
    {
        var saved = settings.DisplayMode switch
        {
            DisplayMode.Normal => settings.NormalPosition,
            DisplayMode.Mini => settings.MiniPosition,
            _ => null
        };
        if (saved == null)
        {
            return;
        }

        var dpi = ScreenHelper.GetDpiForWindow(window.Handle);
        var pixelPos = ScreenHelper.WpfUnitsToPixels(saved.Left, saved.Top, dpi);
        var bounds = ScreenHelper.FindMonitorBoundsForRestore(pixelPos.X, pixelPos.Y, saved.ScreenDeviceName);

        var clampedX = Math.Clamp(pixelPos.X, bounds.X, bounds.X + bounds.Width);
        var clampedY = Math.Clamp(pixelPos.Y, bounds.Y, bounds.Y + bounds.Height);
        window.AppWindow.Move(new(clampedX, clampedY));
    }

    private void OnExitRequested()
    {
        _trayIconService?.Dispose();
        _hotkeyService?.Dispose();
        _mainViewModel?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Environment.Exit(0);
    }

    private void OnHotkeySettingsChanged()
    {
        if (_mainWindow == null || _settingsService == null)
        {
            return;
        }

        var s = _settingsService.Load();
        _hotkeyService?.Update(s.HotkeyModifiers, s.HotkeyVirtualKey);
    }

    private void OnHotkeyTriggered()
    {
        _mainWindow?.ToggleVisibility();
        var hidden = _mainWindow != null && !_mainWindow.AppWindow.IsVisible;
        _trayIconService?.SetHidden(hidden);
        if (hidden)
        {
            _mainViewModel?.SuspendNotifications();
        }
        else
        {
            _mainViewModel?.ResumeNotifications();
        }
    }

    private void OnNotificationRequested(string title, string body)
    {
        // Tray balloon notifications land once TrayIconService exposes a ShowBalloon API (Stage 6/7 follow-up).
    }
}