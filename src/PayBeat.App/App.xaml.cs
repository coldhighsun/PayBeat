using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.ViewModels;
using PayBeat.App.Views;
using Microsoft.Win32;
using System.Windows.Interop;

namespace PayBeat.App;

/// <summary>
/// Application entry point. Owns the top-level object graph: <see cref="Services.SettingsService"/>,
/// <see cref="ViewModels.MainViewModel"/>, <see cref="Views.MainWindow"/>, and <see cref="Services.HotkeyService"/>.
/// Saves window position to settings on exit and manages hotkey suspension while the settings window is open.
/// </summary>
public partial class App
{
    private readonly List<Window> _hiddenWindows = [];
    private HotkeyService? _hotkeyService;
    private MainViewModel? _mainVm;
    private MainWindow? _mainWindow;
    private SettingsService? _settingsService;
    private Mutex? _singleInstanceMutex;
    private SalarySettings? _startupSettings;
    private TrayIconService? _trayIconService;
    private bool _windowsHidden;

    /// <summary>Resumes the global hotkey after it was suspended by the settings window.</summary>
    public void ResumeHotkey() => _hotkeyService?.Resume();

    /// <summary>Suspends the global hotkey while the settings window is capturing key input.</summary>
    public void SuspendHotkey() => _hotkeyService?.Suspend();

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow != null)
        {
            // The window's HWND is already destroyed by the time OnExit runs (WPF closes windows
            // before firing Exit), so the current monitor can no longer be resolved here - use the
            // snapshot MainWindow captured in its Closing handler instead.
            var pos = _mainWindow.LastKnownPosition
                      ?? new WindowPosition(_mainWindow.Left, _mainWindow.Top, ScreenHelper.GetCurrentScreenDeviceName(_mainWindow));
            SaveWindowPosition(pos);
        }

        SystemEvents.SessionEnding -= OnSessionEnding;
        _trayIconService?.Dispose();
        _hotkeyService?.Dispose();
        _mainVm?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService();
        var settings = _settingsService.Load();
        _startupSettings = settings;
        LocalizationService.Apply(settings.Language);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "PayBeat_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                (string)FindResource("Error.AlreadyRunning")!,
                "PayBeat",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }
        SystemEvents.SessionEnding += OnSessionEnding;
        _mainVm = new MainViewModel(_settingsService);
        _mainVm.HotkeySettingsChanged += OnHotkeySettingsChanged;

        _mainWindow = new MainWindow { DataContext = _mainVm };
        _mainWindow.SourceInitialized += (_, _) =>
        {
            var s = _settingsService.Load();
            _hotkeyService = new HotkeyService();
            var registered = _hotkeyService.Register(_mainWindow, s.HotkeyModifiers, s.HotkeyVirtualKey);
            if (!registered)
            {
                var key = HotkeyService.Format(s.HotkeyModifiers, s.HotkeyVirtualKey);
                MessageBox.Show(
                    string.Format((string)FindResource("Error.HotkeyConflict")!, key),
                    "PayBeat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            _hotkeyService.Triggered += ToggleWindowVisibility;
        };
        _mainWindow.ContentRendered += OnMainWindowContentRendered;

        if (settings.DisplayMode == DisplayMode.None)
        {
            // Only create the HWND (for hotkey registration) without showing the window.
            new WindowInteropHelper(_mainWindow).EnsureHandle();
            _mainWindow.ContentRendered -= OnMainWindowContentRendered;
            _trayIconService = new TrayIconService(_mainVm, ActivateMainWindow);
            return;
        }

        _mainWindow.IsRestoringStartupPosition = settings.DisplayMode is DisplayMode.Normal or DisplayMode.Mini or DisplayMode.Flex;
        if (settings.DisplayMode is DisplayMode.Normal or DisplayMode.Mini)
        {
            var startupPos = GetSavedPosition(settings, settings.DisplayMode);
            if (startupPos != null)
            {
                _mainWindow.Left = startupPos.Left;
                _mainWindow.Top = startupPos.Top;
            }
        }

        _mainWindow.Show();
        _trayIconService = new TrayIconService(_mainVm, ActivateMainWindow);
    }

    // Run restore after first render because clamping depends on measured window size.
    private static void ApplyStartupPlacement(MainWindow mainWindow, SalarySettings settings)
    {
        if (settings.DisplayMode == DisplayMode.Flex)
        {
            var flexBounds = ResolveFlexBounds(mainWindow, settings);
            mainWindow.ApplyFlexBounds(flexBounds);
            return;
        }

        var placement = ResolveSavedPlacement(mainWindow, settings, settings.DisplayMode);
        if (placement == null)
        {
            return;
        }

        mainWindow.Left = placement.Value.Left;
        mainWindow.Top = placement.Value.Top;
        mainWindow.ClampToWorkArea(placement.Value.Bounds);
    }

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
    /// Restore Flex by preferred monitor name, falling back to nearest available monitor.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    /// <param name="settings">The salary settings.</param>
    /// <returns>The bounds for the Flex display mode, or null if not available.</returns>
    private static Rect? ResolveFlexBounds(MainWindow mainWindow, SalarySettings settings)
    {
        if (settings.FlexPosition == null)
        {
            return null;
        }

        return ScreenHelper.FindScreenBoundsForRestore(0, 0, settings.FlexPosition.ScreenDeviceName, mainWindow);
    }

    /// <summary>
    /// Resolve saved mode-specific coordinates and target bounds for clamped restore.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    /// <param name="settings">The salary settings.</param>
    /// <param name="mode">The display mode.</param>
    /// <returns>A tuple containing the left, top, and bounds, or null if not available.</returns>
    private static (double Left, double Top, Rect Bounds)? ResolveSavedPlacement(MainWindow mainWindow, SalarySettings settings, DisplayMode mode)
    {
        var pos = GetSavedPosition(settings, mode);
        if (pos == null)
        {
            return null;
        }

        var bounds = ScreenHelper.FindScreenBoundsForRestore(pos.Left, pos.Top, pos.ScreenDeviceName, mainWindow);
        return (pos.Left, pos.Top, bounds);
    }

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

    private void OnHotkeySettingsChanged()
    {
        var s = _settingsService!.Load();
        if (_hotkeyService != null)
        {
            var registered = _hotkeyService.Update(s.HotkeyModifiers, s.HotkeyVirtualKey);
            if (!registered)
            {
                var key = HotkeyService.Format(s.HotkeyModifiers, s.HotkeyVirtualKey);
                MessageBox.Show(
                    string.Format((string)FindResource("Error.HotkeyConflict")!, key),
                    "PayBeat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private void OnMainWindowContentRendered(object? sender, EventArgs e)
    {
        if (_mainWindow == null || _startupSettings == null)
        {
            return;
        }

        _mainWindow.ContentRendered -= OnMainWindowContentRendered;
        ApplyStartupPlacement(_mainWindow, _startupSettings);
        _mainWindow.IsRestoringStartupPosition = false;
    }

    /// <summary>
    /// Proactively saves the window position when Windows is shutting down or the user is logging off.
    /// WPF does not guarantee that <see cref="Window.Closing"/>/<see cref="OnExit"/> run in response to
    /// a session-ending signal, so this subscribes directly rather than relying on that incidental path.
    /// </summary>
    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        if (_mainWindow == null)
        {
            return;
        }

        var pos = new WindowPosition(_mainWindow.Left, _mainWindow.Top, ScreenHelper.GetCurrentScreenDeviceName(_mainWindow));
        SaveWindowPosition(pos);
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
            foreach (Window w in Current.Windows)
            {
                if (w.IsVisible)
                {
                    _hiddenWindows.Add(w);
                    w.Hide();
                }
            }
            _windowsHidden = true;
            _mainVm?.SuspendNotifications();
            _trayIconService?.SetHidden(true);
        }
    }
}