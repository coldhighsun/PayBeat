using PayBeat.App.Services;

namespace PayBeat.App.Tests.Fakes;

/// <summary>
/// Counts <see cref="ISettingsApplier.ReloadSettings"/> calls.
/// </summary>
public sealed class FakeSettingsApplier : ISettingsApplier
{
    /// <summary>
    /// Gets how many times settings were reloaded.
    /// </summary>
    public int ReloadCount
    {
        get; private set;
    }

    /// <inheritdoc/>
    public void ReloadSettings() => ReloadCount++;
}
