using System.Drawing;
using System.Windows.Forms;
using PayBeat.Core.Models;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace PayBeat.WinUI.Services;

/// <summary>
/// Manages the tray icon and its Display Mode / Exit context menu. WinUI3 has no built-in tray
/// API, so this keeps using WinForms <see cref="NotifyIcon"/>/<see cref="ContextMenuStrip"/>
/// directly, same as the WPF build. Menu text comes from <see cref="LocalizationService"/> and is
/// refreshed live on language change, since WinForms controls aren't bindable the way XAML is.
/// Settings/About menu items and the earnings tooltip/balloon notifications are deferred to
/// Stage 6, once a real ViewModel exists to drive them.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MenuItem _displayModeMenuItem;
    private readonly MenuItem _exitMenuItem;
    private readonly MenuItem _flexMenuItem;
    private readonly MenuItem _miniMenuItem;
    private readonly MenuItem _noneMenuItem;
    private readonly MenuItem _normalMenuItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripItem[] _restrictedItems;
    private readonly MainWindow _window;
    private bool _isHidden;

    /// <summary>Creates and shows the tray icon.</summary>
    /// <param name="window">The main widget window the tray menu controls.</param>
    /// <param name="onActivate">Invoked when the user left-clicks the tray icon.</param>
    public TrayIconService(MainWindow window, Action onActivate)
    {
        _window = window;

        _normalMenuItem = new(Text("Menu.Normal"), null, (_, _) => _window.SetDisplayMode(DisplayMode.Normal));
        _miniMenuItem = new(Text("Menu.Mini"), null, (_, _) => _window.SetDisplayMode(DisplayMode.Mini));
        _flexMenuItem = new(Text("Menu.Flex"), null, (_, _) => _window.SetDisplayMode(DisplayMode.Flex));
        _noneMenuItem = new(Text("Menu.None"), null, (_, _) => _window.SetDisplayMode(DisplayMode.None));

        _displayModeMenuItem = new(Text("Menu.DisplayMode"));
        _displayModeMenuItem.DropDownItems.AddRange([_flexMenuItem, _normalMenuItem, _miniMenuItem, _noneMenuItem]);

        var separator = new ToolStripSeparator();
        _exitMenuItem = new(Text("Menu.Exit"), null, (_, _) => ExitRequested?.Invoke());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_displayModeMenuItem);
        contextMenu.Items.Add(separator);
        contextMenu.Items.Add(_exitMenuItem);
        contextMenu.Opening += (_, _) => UpdateDisplayModeChecks();

        _restrictedItems = [_displayModeMenuItem, separator];

        _notifyIcon = new()
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "PayBeat",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && !_isHidden)
            {
                onActivate();
            }
        };

        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>Raised when the user chooses Exit from the tray context menu.</summary>
    public event Action? ExitRequested;

    /// <inheritdoc/>
    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    /// <summary>
    /// Restricts the tray context menu to Exit-only while the widget is hidden via the global
    /// hotkey, or restores it to normal.
    /// </summary>
    /// <param name="isHidden">Whether the widget is currently hidden.</param>
    public void SetHidden(bool isHidden)
    {
        _isHidden = isHidden;
        foreach (var item in _restrictedItems)
        {
            item.Visible = !isHidden;
        }
    }

    private static string Text(string key) => LocalizationService.Instance[key];

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _displayModeMenuItem.Text = Text("Menu.DisplayMode");
        _normalMenuItem.Text = Text("Menu.Normal");
        _miniMenuItem.Text = Text("Menu.Mini");
        _flexMenuItem.Text = Text("Menu.Flex");
        _noneMenuItem.Text = Text("Menu.None");
        _exitMenuItem.Text = Text("Menu.Exit");
    }

    private void UpdateDisplayModeChecks()
    {
        _normalMenuItem.Checked = _window.CurrentDisplayMode == DisplayMode.Normal;
        _miniMenuItem.Checked = _window.CurrentDisplayMode == DisplayMode.Mini;
        _flexMenuItem.Checked = _window.CurrentDisplayMode == DisplayMode.Flex;
        _noneMenuItem.Checked = _window.CurrentDisplayMode == DisplayMode.None;
    }
}