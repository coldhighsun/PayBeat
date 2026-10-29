namespace PayBeat.App.Services;

/// <summary>
/// Lets view models open and close application windows and shut the app down without
/// referencing any view types.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Opens the settings window, or activates it if already open.
    /// </summary>
    void OpenSettings();

    /// <summary>
    /// Opens the about window, or activates it if already open.
    /// </summary>
    void OpenAbout();

    /// <summary>
    /// Closes the settings window if it is open.
    /// </summary>
    void CloseSettings();

    /// <summary>
    /// Shuts the application down.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Suspends the global hotkey while the settings window captures key input.
    /// </summary>
    void SuspendHotkey();

    /// <summary>
    /// Resumes the global hotkey after a prior <see cref="SuspendHotkey"/> call.
    /// </summary>
    void ResumeHotkey();

    /// <summary>
    /// Gets a value indicating whether a global hotkey can be registered on this platform.
    /// </summary>
    bool IsHotkeySupported
    {
        get;
    }
}
