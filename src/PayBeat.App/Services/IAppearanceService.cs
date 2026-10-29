namespace PayBeat.App.Services;

/// <summary>
/// Applies the user's language and color theme to the running application.
/// </summary>
public interface IAppearanceService
{
    /// <summary>
    /// Applies the given language and theme.
    /// </summary>
    /// <param name="language">Language code or <c>"auto"</c>.</param>
    /// <param name="theme">Theme code or <c>"auto"</c>.</param>
    void Apply(string language, string theme);
}
