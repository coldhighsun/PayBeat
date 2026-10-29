using PayBeat.App.Services;

namespace PayBeat.App.Tests.Fakes;

/// <summary>
/// Recording <see cref="IWindowService"/> for view model tests.
/// </summary>
public sealed class FakeWindowService : IWindowService
{
    /// <summary>
    /// Gets how many times the settings window was asked to close.
    /// </summary>
    public int CloseSettingsCount
    {
        get; private set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the fake reports hotkey support.
    /// </summary>
    public bool IsHotkeySupported
    {
        get; set;
    } = true;

    /// <summary>
    /// Gets how many times the hotkey was suspended.
    /// </summary>
    public int SuspendCount
    {
        get; private set;
    }

    /// <summary>
    /// Gets how many times the hotkey was resumed.
    /// </summary>
    public int ResumeCount
    {
        get; private set;
    }

    /// <inheritdoc/>
    public void CloseSettings() => CloseSettingsCount++;

    /// <inheritdoc/>
    public void OpenAbout()
    {
    }

    /// <inheritdoc/>
    public void OpenSettings()
    {
    }

    /// <inheritdoc/>
    public void ResumeHotkey() => ResumeCount++;

    /// <inheritdoc/>
    public void Shutdown()
    {
    }

    /// <inheritdoc/>
    public void SuspendHotkey() => SuspendCount++;
}
