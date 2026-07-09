using PayBeat.WinUI.Services;

namespace PayBeat.WinUI.Views;

/// <summary>
/// Fullscreen "show-off" view with a huge earnings figure and full workday stats. The decorative
/// pulse animation on <see cref="AmountScale"/> lands in Stage 7.
/// </summary>
public sealed partial class FlexView
{
    public FlexView()
    {
        InitializeComponent();
        LocalizationService.Instance.PropertyChanged += (_, _) => Bindings.Update();
    }

    public string DailySalaryLabel => LocalizationService.Instance["View.DailySalary"];
    public string ElapsedLabel => LocalizationService.Instance["View.Elapsed"];
    public string RemainingLabel => LocalizationService.Instance["View.Remaining"];
    public string WorkWindowLabel => LocalizationService.Instance["View.WorkWindow"];
}