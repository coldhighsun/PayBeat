using System.Reflection;

namespace PayBeat.App.Helpers;

/// <summary>
/// Resolves the running app's version from assembly metadata, stripping MinVer's
/// build-metadata suffix (anything after '+').
/// </summary>
public static class AppVersion
{
    /// <summary>The current version string, e.g. <c>1.2.0</c>, or empty if unavailable.</summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

        if (version is null)
        {
            return string.Empty;
        }

        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
