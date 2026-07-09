using PayBeat.App.Services;
using PayBeat.App.ViewModels;
using System.Text.RegularExpressions;

namespace PayBeat.App.Views;

/// <summary>
/// Settings window that lets the user configure salary, work hours, display mode, hotkey, and other preferences.
/// Implements inline hotkey capture: clicking the hotkey field enters capture mode and pressing a key combination
/// commits it; Escape or clicking outside cancels and restores the previous value.
/// </summary>
public partial class SettingsWindow
{
    private static readonly Regex DecimalPattern = new(@"^\d*\.?\d{0,2}$", RegexOptions.Compiled);
    private static readonly Regex IntegerPattern = new(@"^\d*$", RegexOptions.Compiled);

    private bool _hotkeyCommitted;
    private bool _isCapturing;
    private int _savedHotkeyModifiers;
    private int _savedHotkeyVirtualKey;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsWindow"/> and loads the XAML component tree.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private static string BuildModifierPreview(int modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & 0x0002) != 0)
        {
            parts.Add("Ctrl");
        }
        if ((modifiers & 0x0001) != 0)
        {
            parts.Add("Alt");
        }
        if ((modifiers & 0x0004) != 0)
        {
            parts.Add("Shift");
        }
        if ((modifiers & 0x0008) != 0)
        {
            parts.Add("Win");
        }
        return string.Join("+", parts);
    }

    private static void SetHotkeyBoxCapturingStyle(TextBox box, bool capturing)
    {
        if (capturing)
        {
            box.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C3A5E"));
            box.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89B4FA"));
            box.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89B4FA"));
        }
        else
        {
            box.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#313244"));
            box.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#45475A"));
            box.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CDD6F4"));
        }
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).SuspendHotkey();

        if (DataContext is SettingsViewModel vm)
        {
            _savedHotkeyModifiers = vm.HotkeyModifiers;
            _savedHotkeyVirtualKey = vm.HotkeyVirtualKey;
        }
        _hotkeyCommitted = false;
        _isCapturing = true;

        var box = (TextBox)sender;
        SetHotkeyBoxCapturingStyle(box, true);
        box.Text = LocalizationService.Get("Settings.HotkeyCapturing");
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isCapturing = false;
        ((App)Application.Current).ResumeHotkey();

        if (!_hotkeyCommitted && DataContext is SettingsViewModel vm)
        {
            vm.HotkeyModifiers = _savedHotkeyModifiers;
            vm.HotkeyVirtualKey = _savedHotkeyVirtualKey;
        }

        var box = (TextBox)sender;
        SetHotkeyBoxCapturingStyle(box, false);
        if (DataContext is SettingsViewModel restoreVm)
        {
            box.Text = restoreVm.HotkeyDisplayText;
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var box = (TextBox)sender;

        if (key is Key.Escape)
        {
            _hotkeyCommitted = false;
            _isCapturing = false;
            box.MoveFocus(new(FocusNavigationDirection.Next));
            return;
        }

        var modifiers = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            modifiers |= 0x0002;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            modifiers |= 0x0001;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            modifiers |= 0x0004;
        }

        // Show live preview of current modifier state
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
                or Key.Tab)
        {
            box.Text = modifiers > 0
                ? BuildModifierPreview(modifiers) + "+..."
                : LocalizationService.Get("Settings.HotkeyCapturing");
            return;
        }

        // Require at least one modifier to avoid capturing bare keys
        if (modifiers == 0)
        {
            return;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);

        if (DataContext is SettingsViewModel vm)
        {
            vm.HotkeyModifiers = modifiers;
            vm.HotkeyVirtualKey = vk;
        }

        // Clear capture state BEFORE moving focus so Window_PreviewMouseDown
        // no longer treats any subsequent click as an active capture cancellation
        _hotkeyCommitted = true;
        _isCapturing = false;
        box.Text = HotkeyService.Format(modifiers, vk);
        SetHotkeyBoxCapturingStyle(box, false);
        ((App)Application.Current).ResumeHotkey();

        box.MoveFocus(new(FocusNavigationDirection.Next));
    }

    private void Salary_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string))!;
            if (!DecimalPattern.IsMatch(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void Salary_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        var prospective =
            box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, e.Text);

        e.Handled = !DecimalPattern.IsMatch(prospective);
    }

    private void Integer_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string))!;
            if (!IntegerPattern.IsMatch(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void Integer_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        var prospective =
            box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, e.Text);

        e.Handled = !IntegerPattern.IsMatch(prospective);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCapturing && !HotkeyBox.IsMouseOver)
        {
            _hotkeyCommitted = false;
            _isCapturing = false;
            HotkeyBox.MoveFocus(new(FocusNavigationDirection.Next));
        }
    }
}