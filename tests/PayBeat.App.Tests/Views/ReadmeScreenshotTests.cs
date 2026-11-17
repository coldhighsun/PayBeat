using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
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
/// Generates the screenshots used by the README files. It does nothing unless the
/// <c>PAYBEAT_README_SHOTS</c> environment variable points at an output directory.
/// </summary>
public sealed class ReadmeScreenshotTests : IDisposable
{
    /// <summary>
    /// Name of the environment variable holding the output directory.
    /// </summary>
    private const string OutputVariable = "PAYBEAT_README_SHOTS";

    /// <summary>
    /// Temporary settings location so the generator never touches the real user settings.
    /// </summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"PayBeatShots-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// Renders every README screenshot for one language into <c>&lt;output&gt;/&lt;language&gt;</c>.
    /// </summary>
    /// <param name="language">Language code to apply.</param>
    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    public void Generate_WhenOutputDirectoryIsSet_WritesScreenshots(string language)
    {
        if (Environment.GetEnvironmentVariable(OutputVariable) is not { Length: > 0 } root)
        {
            return;
        }

        var output = Path.Combine(root, language);
        Directory.CreateDirectory(output);

        foreach (var theme in new[] { "dark", "light" })
        {
            var suffix = theme == "dark" ? string.Empty : "-light";
            RenderWidget(language, theme, DisplayMode.Normal, output, $"normal{suffix}");
            RenderWidget(language, theme, DisplayMode.Mini, output, $"mini{suffix}");
        }

        RenderWidget(language, "dark", DisplayMode.Flex, output, "flex");

        RenderSettings(language, "dark", output);
        RenderAbout(language, output);
        RenderToast(language, output);

        Directory.GetFiles(output, "*.png").Should().NotBeEmpty();
    }

    /// <summary>
    /// Renders the floating widget in one display mode.
    /// </summary>
    private void RenderWidget(string language, string theme, DisplayMode mode, string output, string name)
    {
        Prepare(language, theme);
        var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
        settings.Save(new SalarySettings { DisplayMode = mode, DailySalary = 1000m, Currency = "¥" });
        using var viewModel = new MainViewModel(settings, new FakeWindowService());
        var window = new MainWindow(new NullTopmostEnforcer()) { DataContext = viewModel, IsRestoringStartupPosition = true };

        window.Show();
        if (mode == DisplayMode.Flex)
        {
            window.Width = 1280;
            window.Height = 720;
        }

        Save(window, output, name);
        window.Close();
    }

    /// <summary>
    /// Renders each tab of the settings window.
    /// </summary>
    private void RenderSettings(string language, string theme, string output)
    {
        string[] tabs = ["salary", "notifications", "display", "system"];
        for (var i = 0; i < tabs.Length; i++)
        {
            Prepare(language, theme);
            var settings = new SettingsService(Path.Combine(_dir, "settings.json"));
            var viewModel = new SettingsViewModel(settings, new FakeSettingsApplier(), new FakeStartupService(), new FakeWindowService(), new FakeAppearanceService());
            var window = new SettingsWindow { DataContext = viewModel };

            window.Show();
            window.FindControl<TabControl>("SettingsTabs")!.SelectedIndex = i;
            Save(window, output, $"settings-{tabs[i]}");
            window.Close();
        }
    }

    /// <summary>
    /// Renders the about window.
    /// </summary>
    private void RenderAbout(string language, string output)
    {
        Prepare(language, "dark");
        var window = new AboutWindow();

        window.Show();
        Save(window, output, "about");
        window.Close();
    }

    /// <summary>
    /// Renders a success and a reminder toast.
    /// </summary>
    private void RenderToast(string language, string output)
    {
        Prepare(language, "dark");
        var success = new ToastWindow(
            LocalizationService.Get("Notification.MilestoneTitle"),
            string.Format(LocalizationService.Get("Notification.MilestoneBody"), "¥300.00"),
            TimeSpan.FromMinutes(5),
            NotificationKind.Success);

        success.Transitions = null;
        success.Show();
        success.Opacity = 1;
        Save(success, output, "toast");
        success.Close();
    }

    /// <summary>
    /// Applies the language and theme the same way production start-up does.
    /// </summary>
    private static void Prepare(string language, string theme)
    {
        LocalizationService.Apply(language);
        ThemeService.Apply(theme);
    }

    /// <summary>
    /// Pumps the UI thread, renders one frame and saves it as a PNG.
    /// </summary>
    private static void Save(Window window, string output, string name)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();

        frame.Should().NotBeNull();
        var path = Path.Combine(output, $"{name}.png");
        var radius = GetOuterCornerRadius(window) * (frame!.PixelSize.Width / window.ClientSize.Width);
        if (radius <= 0)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
            return;
        }

        MakeCornersTransparent(frame, radius);
        frame.Save(path, new PngBitmapEncoderOptions());
    }

    /// <summary>
    /// Returns the corner radius of the outermost rounded border that fills the window, or 0 when there is none.
    /// </summary>
    private static double GetOuterCornerRadius(Window window) =>
        window.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.CornerRadius.TopLeft > 0 && b.Bounds.Width >= window.ClientSize.Width - 1 && b.Bounds.Height >= window.ClientSize.Height - 1)
            .Select(b => b.CornerRadius.TopLeft)
            .FirstOrDefault();

    /// <summary>
    /// Makes the corners outside a rounded rectangle transparent (anti-aliased), as they appear on a real desktop.
    /// Works on the raw pixels so the colors of the rendered frame are left untouched.
    /// </summary>
    private static void MakeCornersTransparent(WriteableBitmap frame, double radius)
    {
        using var buffer = frame.Lock();
        var width = buffer.Size.Width;
        var height = buffer.Size.Height;
        var size = (int)Math.Ceiling(radius);
        var row = new byte[4];

        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                // Distance of the pixel center from the corner arc's center.
                var distance = Math.Sqrt(Math.Pow(radius - dx - 0.5, 2) + Math.Pow(radius - dy - 0.5, 2));
                var coverage = Math.Clamp(radius - distance + 0.5, 0, 1);
                if (coverage >= 1)
                {
                    continue;
                }

                foreach (var (x, y) in new[] { (dx, dy), (width - 1 - dx, dy), (dx, height - 1 - dy), (width - 1 - dx, height - 1 - dy) })
                {
                    var address = buffer.Address + (y * buffer.RowBytes) + (x * 4);
                    System.Runtime.InteropServices.Marshal.Copy(address, row, 0, 4);
                    for (var c = 0; c < 4; c++)
                    {
                        row[c] = (byte)Math.Round(row[c] * coverage);
                    }

                    System.Runtime.InteropServices.Marshal.Copy(row, 0, address, 4);
                }
            }
        }
    }
}
