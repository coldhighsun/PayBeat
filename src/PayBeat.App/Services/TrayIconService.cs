using System.ComponentModel;
using PayBeat.App.ViewModels;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace PayBeat.App.Services;

/// <summary>
/// Manages the tray icon and its context menu, including the "Show" and "Exit" items.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MenuItem _flexMenuItem;
    private readonly MenuItem _miniMenuItem;
    private readonly MenuItem _noneMenuItem;
    private readonly MenuItem _normalMenuItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripItem[] _restrictedItems;
    private readonly MainViewModel _viewModel;
    private bool _isHidden;

    /// <summary>Creates and shows the tray icon.</summary>
    /// <param name="viewModel">Source of the display-mode state and commands the tray menu invokes.</param>
    /// <param name="onActivate">Invoked when the user left-clicks the tray icon.</param>
    public TrayIconService(MainViewModel viewModel, Action onActivate)
    {
        _viewModel = viewModel;

        _normalMenuItem = new(Text("Menu.Normal"), null, (_, _) => _viewModel.SetNormalModeCommand.Execute(null));
        _miniMenuItem = new(Text("Menu.Mini"), null, (_, _) => _viewModel.SetMiniModeCommand.Execute(null));
        _noneMenuItem = new(Text("Menu.None"), null, (_, _) => _viewModel.SetNoneModeCommand.Execute(null));
        _flexMenuItem = new(Text("Menu.Flex"), null, (_, _) => _viewModel.SetFlexModeCommand.Execute(null));

        var displayModeMenuItem = new MenuItem(Text("Menu.DisplayMode"));
        displayModeMenuItem.DropDownItems.AddRange([_flexMenuItem, _normalMenuItem, _miniMenuItem, _noneMenuItem]);

        var separator1 = new ToolStripSeparator();
        var settingsMenuItem = new MenuItem(Text("Menu.Settings"), null, (_, _) => _viewModel.OpenSettingsCommand.Execute(null));
        var aboutMenuItem = new MenuItem(Text("Menu.About"), null, (_, _) => _viewModel.OpenAboutCommand.Execute(null));
        var separator2 = new ToolStripSeparator();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(displayModeMenuItem);
        contextMenu.Items.Add(separator1);
        contextMenu.Items.Add(settingsMenuItem);
        contextMenu.Items.Add(aboutMenuItem);
        contextMenu.Items.Add(separator2);
        contextMenu.Items.Add(new MenuItem(Text("Menu.Exit"), null, (_, _) => _viewModel.ExitCommand.Execute(null)));
        contextMenu.Opening += (_, _) => UpdateDisplayModeChecks();

        _restrictedItems = [displayModeMenuItem, separator1, settingsMenuItem, aboutMenuItem, separator2];

        _notifyIcon = new()
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = TooltipText(),
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
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && !_isHidden)
            {
                _viewModel.OpenSettingsCommand.Execute(null);
            }
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.NotificationRequested += OnNotificationRequested;
    }

    /// <summary>
    /// Restricts the tray icon to a plain "I love work!" tooltip and an Exit-only context menu
    /// while the widget is hidden via the global hotkey, or restores it to normal.
    /// </summary>
    /// <param name="isHidden">Whether the widget is currently hidden.</param>
    public void SetHidden(bool isHidden)
    {
        _isHidden = isHidden;
        foreach (var item in _restrictedItems)
        {
            item.Visible = !isHidden;
        }
        _notifyIcon.Text = isHidden ? HiddenTooltipText() : TooltipText();
        if (isHidden)
        {
            // NotifyIcon has no HideBalloonTip API; toggling Visible dismisses any pending balloon tip.
            _notifyIcon.Visible = false;
            _notifyIcon.Visible = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.NotificationRequested -= OnNotificationRequested;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static string Text(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    private void OnNotificationRequested(string title, string body) =>
        _notifyIcon.ShowBalloonTip(3000, title, body, ToolTipIcon.Info);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EarnedFormatted) && !_isHidden)
        {
            _notifyIcon.Text = TooltipText();
        }
    }

    private static string HiddenTooltipText() => Text("Tray.HiddenTooltip");

    private string TooltipText()
    {
        var text = _viewModel.EarnedFormatted;
        return text.Length > 63 ? text[..63] : text;
    }

    private void UpdateDisplayModeChecks()
    {
        _normalMenuItem.Checked = _viewModel.IsNormalMode;
        _miniMenuItem.Checked = _viewModel.IsMiniMode;
        _noneMenuItem.Checked = _viewModel.IsNoneMode;
        _flexMenuItem.Checked = _viewModel.IsFlexMode;
    }
}