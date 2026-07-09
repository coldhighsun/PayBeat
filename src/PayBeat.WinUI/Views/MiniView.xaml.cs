namespace PayBeat.WinUI.Views;

/// <summary>
/// Minimal view showing only the earnings amount and a thin progress bar.
/// </summary>
public sealed partial class MiniView
{
    public MiniView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Fades only the background fill (not the text/controls above it), so the
    /// <see cref="Helpers.AcrylicBackdropHelper"/>-driven <c>SystemBackdrop</c> behind the window
    /// shows through at lower settings opacity while content stays fully legible.
    /// </summary>
    public void SetBackgroundOpacity(double opacity) => BackgroundBorder.Opacity = opacity;
}