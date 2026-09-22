using GitHubReleaseUpdater.LastCheck;
using GitHubReleaseUpdater.Versioning;

namespace PayBeat.App.Services;

/// <summary>
/// <see cref="ILastCheckStore"/> backed by <see cref="SalarySettings.LastUpdateCheckUtc"/>, so
/// <see cref="ReleaseUpdater"/>'s throttling survives an app restart. The skipped-version half of
/// the interface has no corresponding setting and no UI to set one yet, so it is kept in memory only.
/// </summary>
public sealed class SettingsLastCheckStore(SettingsService settingsService) : ILastCheckStore
{
    private SemanticVersion? _skippedVersion;

    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLastCheckedAtAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(settingsService.Load().LastUpdateCheckUtc);

    /// <inheritdoc />
    public Task<SemanticVersion?> GetSkippedVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_skippedVersion);

    /// <inheritdoc />
    public Task SetLastCheckedAtAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken = default)
    {
        settingsService.Save(settingsService.Load() with { LastUpdateCheckUtc = checkedAt });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetSkippedVersionAsync(SemanticVersion version, CancellationToken cancellationToken = default)
    {
        _skippedVersion = version;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearSkippedVersionAsync(CancellationToken cancellationToken = default)
    {
        _skippedVersion = null;
        return Task.CompletedTask;
    }
}
