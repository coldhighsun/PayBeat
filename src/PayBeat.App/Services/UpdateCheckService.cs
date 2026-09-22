using GitHubReleaseUpdater;
using PayBeat.App.Helpers;

namespace PayBeat.App.Services;

/// <summary>Latest available release, as reported by the GitHub Releases API.</summary>
public sealed record UpdateInfo(string Version, string HtmlUrl);

/// <summary>
/// Checks the project's GitHub Releases feed for a newer stable version than the running build.
/// Best-effort only: any check failure results in a null return. Throttled to once per
/// <see cref="MinimumCheckInterval"/> via <see cref="SettingsLastCheckStore"/>, unless bypassed.
/// </summary>
public sealed class UpdateCheckService(SettingsService settingsService)
{
    private static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(24);

    private const string RepoOwner = "coldhighsun";
    private const string RepoName = "PayBeat";

    /// <summary>
    /// Backing store for the check throttle, reused across calls so skip-version state (once a UI
    /// exists to set it) survives from one check to the next.
    /// </summary>
    private readonly SettingsLastCheckStore _lastCheckStore = new(settingsService);

    /// <summary>
    /// Returns the latest stable release if it is newer than <see cref="AppVersion.Current"/>,
    /// or <c>null</c> if there is no newer release, the check was throttled, or the check failed.
    /// A failed check still records the attempt, so it is throttled the same as a successful one.
    /// </summary>
    /// <param name="bypassThrottle">
    /// True for a user-initiated "check now" that should ignore <see cref="MinimumCheckInterval"/>.
    /// </param>
    public async Task<UpdateInfo?> GetLatestReleaseAsync(bool bypassThrottle = false, CancellationToken cancellationToken = default)
    {
        using var updater = new ReleaseUpdater(new UpdaterOptions
        {
            Owner = RepoOwner,
            Repo = RepoName,
            CurrentVersion = AppVersion.Current,
            LastCheckStore = _lastCheckStore,
            MinimumCheckInterval = MinimumCheckInterval,
        });

        var check = await updater.CheckForUpdateAsync(bypassThrottle: bypassThrottle, cancellationToken: cancellationToken);
        if (!check.Success)
        {
            // ReleaseUpdater only records the check time on success; record it here too so a
            // failure (offline, rate-limited, ...) is throttled the same as a successful check
            // instead of retrying on every app launch.
            await _lastCheckStore.SetLastCheckedAtAsync(DateTimeOffset.UtcNow, cancellationToken);
            return null;
        }

        if (!check.IsUpdateAvailable || check.Update!.Release.HtmlUrl is not { } htmlUrl)
        {
            return null;
        }

        return new UpdateInfo(check.Update.Version.ToString(), htmlUrl);
    }
}
