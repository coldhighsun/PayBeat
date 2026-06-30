using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;

namespace PayBeat.App.Views;

/// <summary>
/// Displays app version, author, and license information.
/// </summary>
public partial class AboutWindow
{
    /// <summary>
    /// Initializes the about window and populates the version label.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = GetVersionString();
    }

    private static string GetVersionString()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        return version is null ? string.Empty : $"v{version}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}