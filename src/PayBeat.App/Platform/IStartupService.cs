namespace PayBeat.App.Platform;

/// <summary>
/// Manages the "run at startup" registration for the current platform.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Gets a value indicating whether the current platform supports the "run at startup" setting.
    /// </summary>
    bool IsSupported
    {
        get;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the app is registered to run at startup,
    /// <see langword="false"/> when it is not, or <see langword="null"/> when the state
    /// couldn't be read (so callers don't mistake "unknown" for "not registered").
    /// </summary>
    /// <returns>The registration state.</returns>
    bool? IsEnabled();

    /// <summary>
    /// Adds or removes the startup registration.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to register; <see langword="false"/> to remove.</param>
    /// <returns><see langword="true"/> if the registration was updated; <see langword="false"/> otherwise.</returns>
    bool SetEnabled(bool enabled);
}
