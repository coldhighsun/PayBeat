using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PayBeat.App.Platform;
using PayBeat.App.ViewModels;
using PayBeat.App.Views;

namespace PayBeat.App.Services;

/// <summary>
/// Desktop implementation of <see cref="IWindowService"/>: creates the settings and about windows,
/// keeps a single instance of each, and shuts the application down.
/// </summary>
/// <param name="settingsService">Settings service handed to the windows it creates.</param>
/// <param name="startupService">Platform startup service handed to the settings view model.</param>
/// <param name="hotkeyService">Hotkey service paused while the settings window captures keys.</param>
/// <param name="desktop">The desktop lifetime that owns all windows.</param>
public sealed class AppWindowService(
    SettingsService settingsService,
    IStartupService startupService,
    IHotkeyService hotkeyService,
    IClassicDesktopStyleApplicationLifetime desktop) : IWindowService
{
    /// <summary>
    /// Gets or sets the main view model; set once it has been created because it depends on this service.
    /// </summary>
    public ISettingsApplier? SettingsApplier
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the main widget window; used as the owner so dialogs stay above a topmost widget.
    /// </summary>
    public Window? MainWindow
    {
        get; set;
    }

    /// <inheritdoc/>
    public bool IsHotkeySupported => hotkeyService.IsSupported;

    /// <inheritdoc/>
    public void CloseSettings()
    {
        desktop.Windows.OfType<SettingsWindow>().FirstOrDefault()?.Close();
    }

    /// <inheritdoc/>
    public void OpenAbout()
    {
        if (desktop.Windows.OfType<AboutWindow>().FirstOrDefault() is { } existing)
        {
            existing.Activate();
            return;
        }

        ShowOwned(new AboutWindow(settingsService));
    }

    /// <inheritdoc/>
    public void OpenSettings()
    {
        if (desktop.Windows.OfType<SettingsWindow>().FirstOrDefault() is { } existing)
        {
            existing.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(settingsService, SettingsApplier!, startupService, this, new AppearanceService());
        ShowOwned(new SettingsWindow { DataContext = viewModel });
    }

    /// <inheritdoc/>
    public void ResumeHotkey() => hotkeyService.Resume();

    /// <inheritdoc/>
    public void Shutdown() => desktop.Shutdown();

    /// <inheritdoc/>
    public void SuspendHotkey() => hotkeyService.Suspend();

    /// <summary>
    /// Shows <paramref name="window"/>. While the widget is pinned on top, the dialog becomes its owned,
    /// topmost child; otherwise it would be buried behind the widget, e.g. in fullscreen Flex mode.
    /// </summary>
    private void ShowOwned(Window window)
    {
        if (MainWindow is { Topmost: true } main)
        {
            window.Topmost = true;
            window.Show(main);
        }
        else
        {
            window.Show();
        }
    }
}
