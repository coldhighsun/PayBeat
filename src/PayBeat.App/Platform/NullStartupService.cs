namespace PayBeat.App.Platform;

/// <summary>
/// No-op <see cref="IStartupService"/> used on platforms where startup registration is not implemented.
/// </summary>
public sealed class NullStartupService : IStartupService
{
    /// <inheritdoc/>
    public bool IsSupported => false;

    /// <inheritdoc/>
    public bool? IsEnabled() => false;

    /// <inheritdoc/>
    public bool SetEnabled(bool enabled) => false;
}
