using PayBeat.App.ViewModels;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace PayBeat.App.Services;

/// <summary>
/// Manages the tray icon and its context menu, including the "Show" and "Exit" items.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MenuItem _compactMenuItem;
    private readonly MenuItem _miniMenuItem;
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

        var displayModeMenuItem = new MenuItem(Text("Menu.DisplayMode"));
        displayModeMenuItem.DropDownItems.AddRange([_normalMenuItem, _compactMenuItem, _miniMenuItem]);

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
            Text = "PayBeat",
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
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static string Text(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    private void UpdateDisplayModeChecks()
    {
        _normalMenuItem.Checked = _viewModel.IsNormalMode;
        _compactMenuItem.Checked = _viewModel.IsCompactMode;
        _miniMenuItem.Checked = _viewModel.IsMiniMode;
    }
}