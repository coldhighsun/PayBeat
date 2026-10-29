using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using PayBeat.App.Models;
using PayBeat.App.Platform;
using PayBeat.App.Services;
using PayBeat.App.Tests.Fakes;
using PayBeat.App.ViewModels;
using PayBeat.App.Views;

namespace PayBeat.App.Tests.Views;

/// <summary>
/// Verifies the decorative looping animations of <see cref="FlexView"/> actually advance over time.
/// </summary>
public sealed class FlexViewAnimationTests : IDisposable
{
    /// <summary>
    /// Temporary settings location so tests never touch the real user settings.
    /// </summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"PayBeatTests-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the amount scale, glow and background pulse change while the Flex view is visible.
    /// </summary>
    [AvaloniaFact]
    public void FlexView_Visible_AnimatesAmountGlowAndBackground()
    {
        LocalizationService.Apply("en");
        ThemeService.Apply("dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        settings.Save(new SalarySettings { DisplayMode = DisplayMode.Flex, DailySalary = 596.25m, Currency = "$" });
        using var viewModel = new MainViewModel(settings, new FakeWindowService());
        var window = new MainWindow(new NullTopmostEnforcer()) { DataContext = viewModel, IsRestoringStartupPosition = true };

        window.Show();
        window.Width = 1280;
        window.Height = 720;
        var flexView = window.GetVisualDescendants().OfType<FlexView>().Single();
        var amount = flexView.FindControl<TextBlock>("AmountText")!;
        var glow = flexView.FindControl<Avalonia.Controls.Shapes.Ellipse>("GlowEllipse")!;
        var pulse = flexView.FindControl<Border>("PulseOverlay")!;

        Dispatcher.UIThread.RunJobs();
        var scale = (ScaleTransform)amount.RenderTransform!;
        var initialScale = scale.ScaleX;
        var initialGlow = glow.Opacity;
        var initialPulse = pulse.Opacity;

        AdvanceAnimations(TimeSpan.FromMilliseconds(600));

        scale.ScaleX.Should().NotBe(initialScale);
        scale.ScaleY.Should().NotBe(initialScale);
        glow.Opacity.Should().NotBe(initialGlow);
        pulse.Opacity.Should().NotBe(initialPulse);
        window.Close();
    }

    /// <summary>
    /// Pumps the UI thread and the render timer until at least <paramref name="duration"/> of wall-clock
    /// time passed. The headless render timer drives the animation clock from real time, so the animation
    /// state is only compared against its own start values (never against absolute positions in the cycle).
    /// </summary>
    /// <param name="duration">Minimum time to let the animations run.</param>
    private static void AdvanceAnimations(TimeSpan duration)
    {
        var step = TimeSpan.FromMilliseconds(50);
        var end = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < end)
        {
            Thread.Sleep(step);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
