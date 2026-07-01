using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Services;
using PayBeat.App.ViewModels;
using PayBeat.App.Views;

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
    private bool _windowsHidden;

    /// <summary>Resumes the global hotkey after it was suspended by the settings window.</summary>
    public void ResumeHotkey() => _hotkeyService?.Resume();

    /// <summary>Suspends the global hotkey while the settings window is capturing key input.</summary>
    public void SuspendHotkey() => _hotkeyService?.Suspend();

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow != null && _mainVm != null && _settingsService != null)
        {
            var deviceName = ScreenHelper.GetCurrentScreenDeviceName(_mainWindow);
            var pos = new WindowPosition(_mainWindow.Left, _mainWindow.Top, deviceName);
            var settings = _settingsService.Load();
            var updated = _mainVm.DisplayMode switch
            {
                DisplayMode.Normal => settings with { NormalPosition = pos },
                DisplayMode.Compact => settings with { CompactPosition = pos },
                DisplayMode.Mini => settings with { MiniPosition = pos },
                _ => settings
            };
            _settingsService.Save(updated);
        }

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
        LocalizationService.Apply(settings.Language);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "PayBeat_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                (string)FindResource("Error.AlreadyRunning"),
                "PayBeat",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }
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
                    string.Format((string)FindResource("Error.HotkeyConflict"), key),
                    "PayBeat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            _hotkeyService.Triggered += ToggleWindowVisibility;
        };
        _mainWindow.Show();

        var pos = GetSavedPosition(settings, settings.DisplayMode);
        if (pos != null)
        {
            _mainWindow.Left = pos.Left;
            _mainWindow.Top = pos.Top;
            var bounds = ScreenHelper.FindScreenBoundsForRestore(pos.Left, pos.Top, pos.ScreenDeviceName, _mainWindow);
            _mainWindow.ClampToWorkArea(bounds);
        }
        else
        {
            _mainWindow.ClampToCurrentScreen();
        }
    }

    private static WindowPosition? GetSavedPosition(SalarySettings settings, DisplayMode mode) =>
        mode switch
        {
            DisplayMode.Normal => settings.NormalPosition,
            DisplayMode.Compact => settings.CompactPosition,
            DisplayMode.Mini => settings.MiniPosition,
            _ => null
        };

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
                    string.Format((string)FindResource("Error.HotkeyConflict"), key),
                    "PayBeat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
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
        }
    }
}