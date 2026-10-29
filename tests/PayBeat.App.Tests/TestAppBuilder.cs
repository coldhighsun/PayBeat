using Avalonia;
using Avalonia.Headless;
using PayBeat.App.Tests;
using PayBeatApp = PayBeat.App.App;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace PayBeat.App.Tests;

/// <summary>
/// Configures the Avalonia application used by headless UI tests: the real <see cref="PayBeatApp"/>
/// (so styles and theme resources load exactly as in production) rendered with Skia.
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    /// Builds the headless application.
    /// </summary>
    /// <returns>The configured app builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<PayBeatApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
