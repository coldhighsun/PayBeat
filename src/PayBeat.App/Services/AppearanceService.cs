namespace PayBeat.App.Services;

/// <summary>
/// Default <see cref="IAppearanceService"/> backed by <see cref="LocalizationService"/> and <see cref="ThemeService"/>.
/// </summary>
public sealed class AppearanceService : IAppearanceService
{
    /// <inheritdoc/>
    public void Apply(string language, string theme)
    {
        LocalizationService.Apply(language);
        ThemeService.Apply(theme);
    }
}
