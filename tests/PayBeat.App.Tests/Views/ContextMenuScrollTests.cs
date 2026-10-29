using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Views;

/// <summary>
/// Verifies context menus never show scroll arrows, even when the popup ends up shorter than the content
/// (which happens by a pixel at fractional display scaling such as 150%).
/// </summary>
public class ContextMenuScrollTests
{
    /// <summary>
    /// Verifies a height-constrained context menu has no visible scroll arrows with the app styles applied.
    /// </summary>
    [AvaloniaFact]
    public void ContextMenu_ShorterThanContent_ShowsNoScrollArrows()
    {
        var (window, menu) = OpenConstrainedMenu();

        var arrows = ScrollArrows(menu);

        arrows.Should().NotBeEmpty();
        arrows.Should().OnlyContain(button => !button.IsEffectivelyVisible);
        window.Close();
    }

    /// <summary>
    /// Control experiment: with scrolling re-enabled on the menu's ScrollViewer the same setup does show the
    /// arrows, which proves the test above can detect them.
    /// </summary>
    [AvaloniaFact]
    public void ContextMenu_ShorterThanContentWithScrollingEnabled_ShowsScrollArrows()
    {
        var (window, menu) = OpenConstrainedMenu(enableScrolling: true);

        ScrollArrows(menu).Should().Contain(button => button.IsEffectivelyVisible);
        window.Close();
    }

    /// <summary>
    /// Opens a context menu with more items than fit into its maximum height.
    /// </summary>
    /// <param name="enableScrolling">Whether to force vertical scrolling back on for the menu's ScrollViewer.</param>
    private static (Window Window, ContextMenu Menu) OpenConstrainedMenu(bool enableScrolling = false)
    {
        LocalizationService.Apply("en");
        ThemeService.Apply("dark");
        var menu = new ContextMenu { MaxHeight = 60 };
        for (var i = 0; i < 8; i++)
        {
            menu.Items.Add(new MenuItem { Header = $"Item {i}" });
        }

        var target = new Border { Width = 200, Height = 100, ContextMenu = menu };
        var window = new Window { Width = 400, Height = 300, Content = target };
        window.Show();
        menu.Open(target);
        Dispatcher.UIThread.RunJobs();

        if (enableScrolling)
        {
            foreach (var viewer in menu.GetVisualDescendants().OfType<ScrollViewer>())
            {
                viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }

            Dispatcher.UIThread.RunJobs();
        }

        return (window, menu);
    }

    /// <summary>
    /// Returns the up/down arrow buttons of the menu's ScrollViewer.
    /// </summary>
    private static List<RepeatButton> ScrollArrows(ContextMenu menu) =>
        menu.GetVisualDescendants().OfType<RepeatButton>().ToList();
}
