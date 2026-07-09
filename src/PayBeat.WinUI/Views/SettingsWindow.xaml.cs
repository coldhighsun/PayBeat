using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using PayBeat.WinUI.Helpers;
using PayBeat.WinUI.Services;
using PayBeat.WinUI.ViewModels;

namespace PayBeat.WinUI.Views;

/// <summary>
/// Settings window. Unlike the WPF build's four-tab layout, this is a single scrollable panel
/// covering the same fields; interactive hotkey capture is deferred to a follow-up (the hotkey is
/// shown read-only here). <see cref="TimePicker"/> works in <see cref="TimeSpan"/>, so work-hours
/// and lunch-break fields are synced to the <see cref="ViewModels.SettingsViewModel"/>'s
/// <see cref="TimeOnly"/> properties manually rather than via a direct binding.
/// </summary>
public sealed partial class SettingsWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        RootGrid.DataContext = viewModel;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        WindowDragHelper.Attach(TitleBar, AppWindow);

        WorkStartPicker.Time = viewModel.WorkStart.ToTimeSpan();
        WorkEndPicker.Time = viewModel.WorkEnd.ToTimeSpan();
        LunchStartPicker.Time = viewModel.LunchBreakStart.ToTimeSpan();
        LunchEndPicker.Time = viewModel.LunchBreakEnd.ToTimeSpan();

        var languageIndex = viewModel.AvailableLanguages
            .Select((lang, i) => (lang, i))
            .FirstOrDefault(x => x.lang.Code == viewModel.Language).i;
        LanguageCombo.SelectedIndex = languageIndex;

        viewModel.CloseRequested += Close;

        LocalizationService.Instance.PropertyChanged += (_, _) => Bindings.Update();
        RootGrid.Loaded += (_, _) => CenterAndResize();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public string AlwaysOnTopLabel => LocalizationService.Instance["Settings.AlwaysOnTop"];
    public string CancelLabel => LocalizationService.Instance["Settings.Cancel"];

    public string DailySalaryLabel => LocalizationService.Instance["Settings.DailySalary"];

    public string DisplaySectionLabel => LocalizationService.Instance["Settings.Tab.Display"];

    public string DisplayModeLabel => LocalizationService.Instance["Settings.DisplayMode"];

    public string EndOfDayReminderLabel => LocalizationService.Instance["Settings.EnableEndOfDayReminder"];

    public string EndOfDayReminderMinutesLabel => LocalizationService.Instance["Settings.EndOfDayReminderMinutes"];

    public string FlexLabel => LocalizationService.Instance["Settings.Flex"];

    public string HotkeyHintLabel => LocalizationService.Instance["Settings.HotkeyHint"];

    public string HotkeyLabel => LocalizationService.Instance["Settings.Hotkey"];

    public string LanguageLabel => LocalizationService.Instance["Settings.Language"];

    public string LunchBreakEnabledLabel => LocalizationService.Instance["Settings.LunchBreakEnabled"];

    public string LunchBreakEndLabel => LocalizationService.Instance["Settings.LunchBreakEnd"];

    public string LunchBreakStartLabel => LocalizationService.Instance["Settings.LunchBreakStart"];

    public string MilestoneAmountLabel => LocalizationService.Instance["Settings.MilestoneAmount"];

    public string MilestoneNotificationsLabel => LocalizationService.Instance["Settings.EnableMilestoneNotifications"];

    public string MiniLabel => LocalizationService.Instance["Settings.Mini"];

    public string NoneLabel => LocalizationService.Instance["Settings.None"];

    public string NormalLabel => LocalizationService.Instance["Settings.Normal"];

    public string NotificationsSectionLabel => LocalizationService.Instance["Settings.Tab.Notifications"];

    public string OpacityLabel => LocalizationService.Instance["Settings.Opacity"];

    public string RefreshIntervalLabel => LocalizationService.Instance["Settings.RefreshInterval"];

    public string RefreshSectionLabel => LocalizationService.Instance["Settings.Section.Refresh"];

    public string RunAtStartupLabel => LocalizationService.Instance["Settings.RunAtStartup"];

    public string SalarySectionLabel => LocalizationService.Instance["Settings.Section.Salary"];

    public string SaveLabel => LocalizationService.Instance["Settings.Save"];

    public string ScheduleSectionLabel => LocalizationService.Instance["Settings.Section.Schedule"];

    public string SystemSectionLabel => LocalizationService.Instance["Settings.Tab.System"];

    public string TitleLabel => LocalizationService.Instance["Settings.Title"];

    public SettingsViewModel ViewModel
    {
        get;
    }

    public string WorkEndLabel => LocalizationService.Instance["View.WorkEnd"];
    public string WorkOnWeekendsLabel => LocalizationService.Instance["Settings.WorkOnWeekends"];
    public string WorkStartLabel => LocalizationService.Instance["View.WorkStart"];

    private void CenterAndResize() => ResizeToContent(RootGrid, centerOnScreen: true, maxHeight: 700);

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.LunchBreakEnabled):
            case nameof(SettingsViewModel.EnableEndOfDayReminder):
            case nameof(SettingsViewModel.EnableMilestoneNotifications):
            case nameof(SettingsViewModel.ErrorMessage):
                CenterAndResize();
                break;
        }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is LanguageOption option)
        {
            ViewModel.Language = option.Code;
        }
    }

    private void LunchEndPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e) =>
        ViewModel.LunchBreakEnd = TimeOnly.FromTimeSpan(e.NewTime);

    private void LunchStartPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e) =>
        ViewModel.LunchBreakStart = TimeOnly.FromTimeSpan(e.NewTime);

    private void WorkEndPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e) =>
        ViewModel.WorkEnd = TimeOnly.FromTimeSpan(e.NewTime);

    private void WorkStartPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e) =>
        ViewModel.WorkStart = TimeOnly.FromTimeSpan(e.NewTime);
}