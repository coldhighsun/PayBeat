using PayBeat.App.Platform;

namespace PayBeat.App.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IStartupService"/> for view model tests.
/// </summary>
public sealed class FakeStartupService : IStartupService
{
    /// <summary>
    /// Gets or sets a value indicating whether startup registration is currently enabled.
    /// </summary>
    public bool? Enabled
    {
        get; set;
    } = false;

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="SetEnabled"/> succeeds.
    /// </summary>
    public bool SetSucceeds
    {
        get; set;
    } = true;

    /// <inheritdoc/>
    public bool IsSupported => true;

    /// <inheritdoc/>
    public bool? IsEnabled() => Enabled;

    /// <inheritdoc/>
    public bool SetEnabled(bool enabled)
    {
        if (SetSucceeds)
        {
            Enabled = enabled;
        }

        return SetSucceeds;
    }
}
