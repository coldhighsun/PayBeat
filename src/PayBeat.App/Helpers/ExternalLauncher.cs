using System.Diagnostics;

namespace PayBeat.App.Helpers;

/// <summary>
/// Opens URLs in the user's default browser on every supported platform.
/// </summary>
public static class ExternalLauncher
{
    /// <summary>
    /// Opens <paramref name="url"/> with the shell's default handler. Only absolute http(s) URLs are
    /// accepted; anything else is ignored. Launch failures are swallowed because opening a link is best-effort.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    public static void Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // Best-effort; there may be no default browser registered.
        }
    }
}
