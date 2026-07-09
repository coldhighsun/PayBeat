using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PayBeat.WinUI.Helpers;
using PayBeat.WinUI.Services;
using System.Reflection;

namespace PayBeat.WinUI.Views;

/// <summary>
/// "About" dialog, centered on the primary monitor. Ports the WPF build's <c>AboutWindow</c>
/// onto <see cref="MainWindow"/>'s content pattern.
/// </summary>
public sealed partial class AboutWindow
{
    public AboutWindow()
    {
        InitializeComponent();

        VersionText.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        WindowDragHelper.Attach(RootGrid, AppWindow);

        RootGrid.Loaded += (_, _) => CenterAndResize();
    }

    public string AuthorLabel => LocalizationService.Instance["About.Author"];
    public string CloseLabel => LocalizationService.Instance["About.Close"];
    public string DescriptionLabel => LocalizationService.Instance["About.Description"];
    public string GitHubLabel => LocalizationService.Instance["About.GitHub"];
    public string LicenseLabel => LocalizationService.Instance["About.License"];

    private void CenterAndResize() => ResizeToContent(RootGrid, centerOnScreen: true);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}