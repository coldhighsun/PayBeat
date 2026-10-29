using PayBeat.App.Services;

namespace PayBeat.App.Tests.Fakes;

/// <summary>
/// Records the language and theme passed to <see cref="IAppearanceService.Apply"/> instead of touching the application.
/// </summary>
public sealed class FakeAppearanceService : IAppearanceService
{
    /// <summary>
    /// Gets the language and theme pairs that were applied, in order.
    /// </summary>
    public List<(string Language, string Theme)> Applied { get; } = [];

    /// <inheritdoc/>
    public void Apply(string language, string theme) => Applied.Add((language, theme));
}
