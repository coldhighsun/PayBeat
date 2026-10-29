using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using PayBeat.App.ViewModels;

namespace PayBeat.App.Services;

/// <summary>
/// Manages the tray icon and its context menu (display modes, settings, about and exit).
/// Notifications are shown by <see cref="ToastService"/>, not by the tray icon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    /// <summary>
    /// Location of the tray icon image.
    /// </summary>
    private const string IconUri = "avares://PayBeat/Resources/Icons/PayBeat.png";

    /// <summary>
    /// Menu item that switches to flex mode.
    /// </summary>
    private readonly NativeMenuItem _flexMenuItem;

    /// <summary>
    /// Menu item that switches to mini mode.
    /// </summary>
    private readonly NativeMenuItem _miniMenuItem;

    /// <summary>
    /// Menu item that hides the widget.
    /// </summary>
    private readonly NativeMenuItem _noneMenuItem;

    /// <summary>
    /// Menu item that switches to normal mode.
    /// </summary>
    private readonly NativeMenuItem _normalMenuItem;

    /// <summary>
    /// The native tray icon.
    /// </summary>
    private readonly TrayIcon _trayIcon;

    /// <summary>
    /// The complete context menu.
    /// </summary>
    private readonly NativeMenu _fullMenu;

    /// <summary>
    /// The Exit-only context menu shown while the widget is hidden via the global hotkey.
    /// </summary>
    private readonly NativeMenu _exitOnlyMenu;

    /// <summary>
    /// Source of the display-mode state and commands the tray menu invokes.
    /// </summary>
    private readonly MainViewModel _viewModel;

    /// <summary>
    /// Whether the widget is currently hidden via the global hotkey.
    /// </summary>
    private bool _isHidden;

    /// <summary>
    /// Creates and shows the tray icon.
    /// </summary>
    /// <param name="viewModel">Source of the display-mode state and commands the tray menu invokes.</param>
    /// <param name="onActivate">Invoked when the user clicks the tray icon.</param>
    public TrayIconService(MainViewModel viewModel, Action onActivate)
    {
        _viewModel = viewModel;

        _flexMenuItem = CreateCheckItem("Menu.Flex", _viewModel.SetFlexModeCommand);
        _normalMenuItem = CreateCheckItem("Menu.Normal", _viewModel.SetNormalModeCommand);
        _miniMenuItem = CreateCheckItem("Menu.Mini", _viewModel.SetMiniModeCommand);
        _noneMenuItem = CreateCheckItem("Menu.None", _viewModel.SetNoneModeCommand);

        var displayModeMenuItem = new NativeMenuItem(LocalizationService.Get("Menu.DisplayMode"))
        {
            Menu = [_flexMenuItem, _normalMenuItem, _miniMenuItem, _noneMenuItem],
        };
        var separator1 = new NativeMenuItemSeparator();
        var settingsMenuItem = CreateItem("Menu.Settings", _viewModel.OpenSettingsCommand);
        var aboutMenuItem = CreateItem("Menu.About", _viewModel.OpenAboutCommand);
        var separator2 = new NativeMenuItemSeparator();

        _fullMenu =
        [
            displayModeMenuItem,
            separator1,
            settingsMenuItem,
            aboutMenuItem,
            separator2,
            CreateItem("Menu.Exit", _viewModel.ExitCommand),
        ];
        _exitOnlyMenu = [CreateItem("Menu.Exit", _viewModel.ExitCommand)];

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(IconUri))),
            ToolTipText = TooltipText(),
            Menu = _fullMenu,
            IsVisible = true,
        };
        _trayIcon.Clicked += (_, _) =>
        {
            if (!_isHidden)
            {
                onActivate();
            }
        };

        TrayIcon.SetIcons(Application.Current!, [_trayIcon]);

        UpdateDisplayModeChecks();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Restricts the tray icon to a plain "I love work!" tooltip and an Exit-only context menu
    /// while the widget is hidden via the global hotkey, or restores it to normal.
    /// </summary>
    /// <param name="isHidden">Whether the widget is currently hidden.</param>
    public void SetHidden(bool isHidden)
    {
        _isHidden = isHidden;
        _trayIcon.Menu = isHidden ? _exitOnlyMenu : _fullMenu;
        _trayIcon.ToolTipText = isHidden ? LocalizationService.Get("Tray.HiddenTooltip") : TooltipText();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }

    /// <summary>
    /// Creates a plain menu item bound to <paramref name="command"/>.
    /// </summary>
    private static NativeMenuItem CreateItem(string resourceKey, ICommand command) =>
        new(LocalizationService.Get(resourceKey)) { Command = command };

    /// <summary>
    /// Creates a checkable menu item bound to <paramref name="command"/>. The check mark is driven
    /// by <see cref="UpdateDisplayModeChecks"/>, so a click never toggles it on its own.
    /// </summary>
    private static NativeMenuItem CreateCheckItem(string resourceKey, ICommand command) =>
        new(LocalizationService.Get(resourceKey))
        {
            Command = command,
            ToggleType = MenuItemToggleType.CheckBox,
        };

    /// <summary>
    /// Keeps the tooltip and check marks in sync with the view model.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EarnedFormatted) && !_isHidden)
        {
            _trayIcon.ToolTipText = TooltipText();
        }
        else if (e.PropertyName == nameof(MainViewModel.DisplayMode))
        {
            UpdateDisplayModeChecks();
        }
    }

    /// <summary>
    /// Returns the live earnings text used as the tooltip, truncated to the Win32 limit of 63 characters.
    /// </summary>
    private string TooltipText()
    {
        var text = _viewModel.EarnedFormatted;
        return text.Length > 63 ? text[..63] : text;
    }

    /// <summary>
    /// Updates the display-mode check marks from the view model.
    /// </summary>
    private void UpdateDisplayModeChecks()
    {
        _normalMenuItem.IsChecked = _viewModel.IsNormalMode;
        _miniMenuItem.IsChecked = _viewModel.IsMiniMode;
        _noneMenuItem.IsChecked = _viewModel.IsNoneMode;
        _flexMenuItem.IsChecked = _viewModel.IsFlexMode;
    }
}
