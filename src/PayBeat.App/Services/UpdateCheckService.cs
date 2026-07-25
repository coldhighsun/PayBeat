using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using PayBeat.App.Helpers;

namespace PayBeat.App.Services;

/// <summary>Latest available release, as reported by the GitHub Releases API.</summary>
public sealed record UpdateInfo(string Version, string HtmlUrl);

/// <summary>
/// Checks the project's GitHub Releases feed for a newer stable version than the running build.
/// Best-effort only: any network, parsing, or version-comparison failure results in a null return.
/// </summary>
public sealed class UpdateCheckService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/coldhighsun/PayBeat/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    /// <summary>
    /// Returns the latest stable release if it is newer than <see cref="AppVersion.Current"/>,
    /// or <c>null</c> if there is no newer release or the check failed.
    /// </summary>
    public async Task<UpdateInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync(LatestReleaseUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var tagName = json.RootElement.GetProperty("tag_name").GetString();
            var htmlUrl = json.RootElement.GetProperty("html_url").GetString();
            if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(htmlUrl))
            {
                return null;
            }

            var latestVersionText = tagName.StartsWith('v') ? tagName[1..] : tagName;
            if (!Version.TryParse(latestVersionText, out var latestVersion)
                || !Version.TryParse(AppVersion.Current, out var currentVersion)
                || latestVersion <= currentVersion)
            {
                return null;
            }

            return new UpdateInfo(latestVersionText, htmlUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PayBeat", AppVersion.Current));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}