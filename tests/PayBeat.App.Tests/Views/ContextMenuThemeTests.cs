using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Views;

/// <summary>
/// Verifies context menus follow the requested light/dark theme.
/// </summary>
public class ContextMenuThemeTests
{
    /// <summary>
    /// Verifies the opened menu resolves the requested theme variant and uses the palette colors.
    /// </summary>
    /// <param name="theme">Theme code to apply.</param>
    /// <param name="expectedBackground">Expected menu surface color as a hex string.</param>
    [AvaloniaTheory]
    [InlineData("dark", "#FF313244")]
    [InlineData("light", "#FFE6E9EF")]
    public void ContextMenu_Opened_UsesThemePalette(string theme, string expectedBackground)
    {
        LocalizationService.Apply("en");
        ThemeService.Apply(theme);
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Item" });
        var target = new Border { Width = 200, Height = 100, ContextMenu = menu };
        var window = new Window { Width = 400, Height = 300, Content = target };
        window.Show();
        menu.Open(target);
        Dispatcher.UIThread.RunJobs();

        var expectedVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        menu.ActualThemeVariant.Should().Be(expectedVariant);
        var background = menu.GetVisualDescendants().OfType<Border>()
            .Select(border => border.Background).OfType<ISolidColorBrush>().FirstOrDefault();
        background.Should().NotBeNull();
        background!.Color.Should().Be(Color.Parse(expectedBackground));
        window.Close();
        ThemeService.Apply("auto");
    }
}
