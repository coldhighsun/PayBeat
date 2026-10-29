using Avalonia.Styling;
using AwesomeAssertions;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Services;

/// <summary>
/// Tests for <see cref="ThemeService"/>.
/// </summary>
public class ThemeServiceTests
{
    /// <summary>
    /// Verifies theme codes map to the matching Avalonia variants.
    /// </summary>
    /// <param name="code">Theme code stored in settings.</param>
    /// <param name="expectDark">Whether the dark variant is expected.</param>
    [Theory]
    [InlineData("dark", true)]
    [InlineData("light", false)]
    public void ToVariant_ExplicitTheme_ReturnsMatchingVariant(string code, bool expectDark)
    {
        var variant = ThemeService.ToVariant(code);

        variant.Should().Be(expectDark ? ThemeVariant.Dark : ThemeVariant.Light);
    }

    /// <summary>
    /// Verifies "auto" and unknown codes follow the operating system.
    /// </summary>
    /// <param name="code">Theme code stored in settings.</param>
    [Theory]
    [InlineData("auto")]
    [InlineData("")]
    [InlineData("solarized")]
    public void ToVariant_AutoOrUnknown_FollowsSystem(string code)
    {
        ThemeService.ToVariant(code).Should().Be(ThemeVariant.Default);
    }
}
