using PayBeat.WinUI.Services;

namespace PayBeat.WinUI.Views;

/// <summary>
/// Full-size view showing earnings, progress bar, and work-hours summary.
/// </summary>
public sealed partial class NormalView
{
    public NormalView()
    {
        InitializeComponent();
        LocalizationService.Instance.PropertyChanged += (_, _) => Bindings.Update();
    }

    public string DailySalaryLabel => LocalizationService.Instance["View.DailySalary"];
    public string WorkEndLabel => LocalizationService.Instance["View.WorkEnd"];
    public string WorkStartLabel => LocalizationService.Instance["View.WorkStart"];

    /// <summary>
    /// Fades only the background fill (not the text/controls above it), so the
    /// <see cref="Helpers.AcrylicBackdropHelper"/>-driven <c>SystemBackdrop</c> behind the window
    /// shows through at lower settings opacity while content stays fully legible.
    /// </summary>
    public void SetBackgroundOpacity(double opacity) => BackgroundBorder.Opacity = opacity;
}