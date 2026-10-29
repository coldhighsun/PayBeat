namespace PayBeat.App.Services;

/// <summary>
/// Receives a notification after settings were saved so the running widget can apply them.
/// </summary>
public interface ISettingsApplier
{
    /// <summary>
    /// Reloads settings from disk and refreshes everything derived from them.
    /// </summary>
    void ReloadSettings();
}
