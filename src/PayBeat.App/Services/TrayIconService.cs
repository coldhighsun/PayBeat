using System.ComponentModel;
using PayBeat.App.ViewModels;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace PayBeat.App.Services;

/// <summary>
/// Manages the tray icon and its context menu, including the "Show" and "Exit" items.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MenuItem _compactMenuItem;
    private readonly MenuItem _flexMenuItem;
    private readonly MenuItem _miniMenuItem;
    private readonly MenuItem _noneMenuItem;
    private readonly MenuItem _normalMenuItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly MainViewModel _viewModel;

    /// <summary>Creates and shows the tray icon.</summary>
    /// <param name="viewModel">Source of the display-mode state and commands the tray menu invokes.</param>
    /// <param name="onActivate">Invoked when the user left-clicks the tray icon.</param>
    public TrayIconService(MainViewModel viewModel, Action onActivate)
    {
        _viewModel = viewModel;

        _normalMenuItem = new MenuItem(Text("Menu.Normal"), null, (_, _) => _viewModel.SetNormalModeCommand.Execute(null));
        _compactMenuItem = new MenuItem(Text("Menu.Compact"), null, (_, _) => _viewModel.SetCompactModeCommand.Execute(null));
        _miniMenuItem = new MenuItem(Text("Menu.Mini"), null, (_, _) => _viewModel.SetMiniModeCommand.Execute(null));
        _noneMenuItem = new MenuItem(Text("Menu.None"), null, (_, _) => _viewModel.SetNoneModeCommand.Execute(null));
        _flexMenuItem = new MenuItem(Text("Menu.Flex"), null, (_, _) => _viewModel.SetFlexModeCommand.Execute(null));

        var displayModeMenuItem = new MenuItem(Text("Menu.DisplayMode"));
        displayModeMenuItem.DropDownItems.AddRange([_flexMenuItem, _normalMenuItem, _compactMenuItem, _miniMenuItem, _noneMenuItem]);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(displayModeMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new MenuItem(Text("Menu.Settings"), null, (_, _) => _viewModel.OpenSettingsCommand.Execute(null)));
        contextMenu.Items.Add(new MenuItem(Text("Menu.About"), null, (_, _) => _viewModel.OpenAboutCommand.Execute(null)));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new MenuItem(Text("Menu.Exit"), null, (_, _) => _viewModel.ExitCommand.Execute(null)));
        contextMenu.Opening += (_, _) => UpdateDisplayModeChecks();

        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = TooltipText(),
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                onActivate();
            }
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static string Text(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EarnedFormatted))
        {
            _notifyIcon.Text = TooltipText();
        }
    }

    private string TooltipText()
    {
        var text = _viewModel.EarnedFormatted;
        return text.Length > 63 ? text[..63] : text;
    }

    private void UpdateDisplayModeChecks()
    {
        _normalMenuItem.Checked = _viewModel.IsNormalMode;
        _compactMenuItem.Checked = _viewModel.IsCompactMode;
        _miniMenuItem.Checked = _viewModel.IsMiniMode;
        _noneMenuItem.Checked = _viewModel.IsNoneMode;
        _flexMenuItem.Checked = _viewModel.IsFlexMode;
    }
}