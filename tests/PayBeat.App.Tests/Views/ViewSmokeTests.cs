using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
/// Headless smoke tests: every window must load its XAML, bind to its view model and render a frame.
/// Set <c>PAYBEAT_TEST_SHOTS</c> to a directory to also save the rendered frames as PNG files.
/// </summary>
public sealed class ViewSmokeTests : IDisposable
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
    /// Verifies the widget renders in every visible display mode.
    /// </summary>
    /// <param name="mode">Display mode to render.</param>
    [AvaloniaTheory]
    [InlineData(DisplayMode.Normal)]
    [InlineData(DisplayMode.Mini)]
    [InlineData(DisplayMode.Flex)]
    public void MainWindow_DisplayMode_RendersFrame(DisplayMode mode)
    {
        PrepareApplication("en", "dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        settings.Save(new SalarySettings { DisplayMode = mode, DailySalary = 596.25m, Currency = "$" });
        using var viewModel = new MainViewModel(settings, new FakeWindowService());
        var window = new MainWindow(new NullTopmostEnforcer()) { DataContext = viewModel, IsRestoringStartupPosition = true };

        window.Show();
        if (mode == DisplayMode.Flex)
        {
            window.Width = 1280;
            window.Height = 720;
        }

        var frame = Render(window, $"main-{mode}");

        frame.Should().NotBeNull();
        frame!.PixelSize.Width.Should().BeGreaterThan(0);
        window.Close();
    }

    /// <summary>
    /// Verifies the mini widget is only as wide as the full daily salary text, so it never resizes during the day.
    /// </summary>
    [AvaloniaFact]
    public void MainWindow_MiniMode_WidthFollowsDailySalaryText()
    {
        PrepareApplication("en", "dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        settings.Save(new SalarySettings { DisplayMode = DisplayMode.Mini, DailySalary = 596.25m, Currency = "$" });
        using var viewModel = new MainViewModel(settings, new FakeWindowService());
        var window = new MainWindow(new NullTopmostEnforcer()) { DataContext = viewModel, IsRestoringStartupPosition = true };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.Bounds.Width.Should().BeLessThan(120);
        window.Close();
    }

    /// <summary>
    /// Verifies the mini widget settles on its content width right away instead of shrinking step by step
    /// from its initial size (the progress bar's indicator width used to feed back into the window width).
    /// </summary>
    [AvaloniaFact]
    public void MainWindow_MiniMode_FirstLayout_SettlesWithoutStepwiseShrink()
    {
        PrepareApplication("en", "dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));

        // A work window around the clock on every day keeps the progress strictly between 0 and 1.
        settings.Save(new SalarySettings
        {
            DisplayMode = DisplayMode.Mini,
            DailySalary = 596.25m,
            Currency = "$",
            WorkStart = new TimeOnly(0, 0),
            WorkEnd = new TimeOnly(23, 59),
            WorkOnWeekends = true,
        });
        using var viewModel = new MainViewModel(settings, new FakeWindowService());
        var window = new MainWindow(new NullTopmostEnforcer()) { DataContext = viewModel, IsRestoringStartupPosition = true };

        var widths = new List<double>();
        window.SizeChanged += (_, e) => widths.Add(e.NewSize.Width);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The first entry is the platform's default window size; the window must then settle on its content
        // width in a couple of layout passes instead of shrinking step by step.
        widths.Count.Should().BeLessThanOrEqualTo(3, $"the widths were {string.Join(", ", widths)}");        window.Bounds.Width.Should().BeLessThan(120);
        window.Close();
    }

    /// <summary>
    /// Verifies the mini progress fill is scaled to the view model's workday progress.
    /// </summary>
    [AvaloniaFact]
    public void MiniView_ProgressChanges_ScalesProgressFill()
    {
        PrepareApplication("en", "dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        settings.Save(new SalarySettings
        {
            DisplayMode = DisplayMode.Mini,
            DailySalary = 596.25m,
            Currency = "$",
            WorkStart = new TimeOnly(0, 0),
            WorkEnd = new TimeOnly(23, 59),
            WorkOnWeekends = true,
        });
        using var viewModel = new MainViewModel(settings, new FakeWindowService());
        var window = new MainWindow(new NullTopmostEnforcer()) { DataContext = viewModel, IsRestoringStartupPosition = true };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        var fill = window.GetVisualDescendants().OfType<MiniView>().Single().FindControl<Border>("ProgressFill")!;
        var scale = (Avalonia.Media.ScaleTransform)fill.RenderTransform!;

        viewModel.Progress.Should().BeInRange(0.0001, 0.9999);
        scale.ScaleX.Should().BeApproximately(viewModel.Progress, 0.0001);
        window.Close();
    }

    /// <summary>
    /// Verifies the settings window renders with its view model and every tab.
    /// </summary>
    /// <param name="tabIndex">Index of the tab to select.</param>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SettingsWindow_EachTab_RendersFrame(int tabIndex)
    {
        PrepareApplication("en", "dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        var viewModel = new SettingsViewModel(settings, new FakeSettingsApplier(), new FakeStartupService(), new FakeWindowService(), new FakeAppearanceService());
        var window = new SettingsWindow { DataContext = viewModel };

        window.Show();
        window.FindControl<TabControl>("SettingsTabs")!.SelectedIndex = tabIndex;
        var frame = Render(window, $"settings-{tabIndex}");

        frame.Should().NotBeNull();
        window.Close();
    }

    /// <summary>
    /// Verifies clicking a drop-down opens it instead of starting a window drag.
    /// </summary>
    /// <param name="tabIndex">Tab that hosts the combo box.</param>
    [AvaloniaTheory]
    [InlineData(2)]
    [InlineData(3)]
    public void SettingsWindow_ClickComboBox_OpensDropDown(int tabIndex)
    {
        PrepareApplication("en", "dark");
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        var viewModel = new SettingsViewModel(settings, new FakeSettingsApplier(), new FakeStartupService(), new FakeWindowService(), new FakeAppearanceService());
        var window = new SettingsWindow { DataContext = viewModel };
        window.Show();
        window.FindControl<TabControl>("SettingsTabs")!.SelectedIndex = tabIndex;
        Dispatcher.UIThread.RunJobs();
        var combo = window.GetVisualDescendants().OfType<ComboBox>().First(c => c.IsEffectivelyVisible);
        var center = CenterInWindow(combo, window);

        window.MouseDown(center, Avalonia.Input.MouseButton.Left);
        window.MouseUp(center, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        combo.IsDropDownOpen.Should().BeTrue();
        window.Close();
    }

    /// <summary>
    /// Verifies the message window renders its text.
    /// </summary>
    [AvaloniaFact]
    public void MessageWindow_WithText_RendersFrame()
    {
        PrepareApplication("zh-CN", "light");
        var window = new MessageWindow("PayBeat", LocalizationService.Get("Error.AlreadyRunning"));

        window.Show();
        var frame = Render(window, "message-light");

        frame.Should().NotBeNull();
        window.Close();
    }

    /// <summary>
    /// Applies the language and theme the same way production start-up does.
    /// </summary>
    private static void PrepareApplication(string language, string theme)
    {
        LocalizationService.Apply(language);
        ThemeService.Apply(theme);
    }

    /// <summary>
    /// Returns the center of <paramref name="control"/> in window coordinates by summing ancestor offsets.
    /// </summary>
    private static Avalonia.Point CenterInWindow(Control control, Window window)
    {
        var x = control.Bounds.Width / 2;
        var y = control.Bounds.Height / 2;
        for (Avalonia.Visual? v = control; v is not null && v != window; v = v.GetVisualParent())
        {
            x += v.Bounds.X;
            y += v.Bounds.Y;
        }

        return new Avalonia.Point(x, y);
    }

    /// <summary>
    /// Pumps the UI thread, renders one frame and optionally saves it.
    /// </summary>
    private static Avalonia.Media.Imaging.WriteableBitmap? Render(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();

        if (Environment.GetEnvironmentVariable("PAYBEAT_TEST_SHOTS") is { Length: > 0 } shots && frame is not null)
        {
            Directory.CreateDirectory(shots);
            frame.Save(Path.Combine(shots, $"{name}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        return frame;
    }
}
