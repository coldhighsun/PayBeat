using System.Text.RegularExpressions;
using Avalonia.Controls;
using PayBeat.App.Helpers;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using PayBeat.App.Platform;
using PayBeat.App.Services;
using PayBeat.App.ViewModels;

namespace PayBeat.App.Views;

/// <summary>
/// Settings window that lets the user configure salary, work hours, display mode, hotkey, and other preferences.
/// Implements inline hotkey capture: clicking the hotkey field enters capture mode and pressing a key combination
/// commits it; Escape or clicking outside cancels and restores the previous value.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// Accepts an optional decimal number with at most two fractional digits.
    /// </summary>
    private static readonly Regex DecimalPattern = new(@"^\d*\.?\d{0,2}$", RegexOptions.Compiled);

    /// <summary>
    /// Accepts digits only.
    /// </summary>
    private static readonly Regex IntegerPattern = new(@"^\d*$", RegexOptions.Compiled);

    /// <summary>
    /// Whether the hotkey captured in the current session was committed.
    /// </summary>
    private bool _hotkeyCommitted;

    /// <summary>
    /// Whether the hotkey box is currently capturing a key combination.
    /// </summary>
    private bool _isCapturing;

    /// <summary>
    /// Hotkey modifiers before capture started, restored when capture is cancelled.
    /// </summary>
    private int _savedHotkeyModifiers;

    /// <summary>
    /// Hotkey virtual key before capture started, restored when capture is cancelled.
    /// </summary>
    private int _savedHotkeyVirtualKey;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsWindow"/> and loads the XAML component tree.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, Window_PreviewPointerPressed, RoutingStrategies.Tunnel);
        PointerPressed += Window_PointerPressed;

        AttachInputFilter(SalaryBox, DecimalPattern);
        AttachInputFilter(MilestoneBox, DecimalPattern);
        AttachInputFilter(EndOfDayMinutesBox, IntegerPattern);

        HotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);
        HotkeyBox.GotFocus += HotkeyBox_GotFocus;
        HotkeyBox.LostFocus += HotkeyBox_LostFocus;

        Opened += (_, _) =>
        {
            SizeToDevicePixels(NormalPreview);
            SizeToDevicePixels(MiniPreview);
        };
    }

    /// <summary>
    /// Sizes an image so one bitmap pixel maps to exactly one device pixel, which keeps small
    /// screenshots crisp on scaled (e.g. 125%) displays instead of being resampled.
    /// </summary>
    /// <param name="image">The image whose source bitmap is shown at its real pixel size.</param>
    private void SizeToDevicePixels(Image image)
    {
        if (image.Source is Avalonia.Media.Imaging.Bitmap source)
        {
            image.Width = source.PixelSize.Width / RenderScaling;
            image.Height = source.PixelSize.Height / RenderScaling;
        }
    }

    /// <summary>
    /// Restricts what can be typed or pasted into <paramref name="box"/> to text matching <paramref name="pattern"/>.
    /// </summary>
    /// <param name="box">The text box to filter.</param>
    /// <param name="pattern">Pattern the whole resulting text must match.</param>
    private static void AttachInputFilter(TextBox box, Regex pattern)
    {
        box.AddHandler(
            TextInputEvent,
            (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Text))
                {
                    e.Handled = !pattern.IsMatch(Prospective(box, e.Text));
                }
            },
            RoutingStrategies.Tunnel);

        box.PastingFromClipboard += async (_, e) =>
        {
            // Take over pasting so the clipboard text can be validated before it reaches the box.
            e.Handled = true;
            var clipboard = TopLevel.GetTopLevel(box)?.Clipboard;
            var text = clipboard is null ? null : await clipboard.TryGetTextAsync();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var prospective = Prospective(box, text);
            if (!pattern.IsMatch(prospective) || (box.MaxLength > 0 && prospective.Length > box.MaxLength))
            {
                return;
            }

            var caret = Math.Min(box.SelectionStart, box.SelectionEnd) + text.Length;
            box.Text = prospective;
            box.CaretIndex = caret;
        };
    }

    /// <summary>
    /// Returns the text <paramref name="box"/> would contain after replacing its selection with <paramref name="input"/>.
    /// </summary>
    private static string Prospective(TextBox box, string input)
    {
        var text = box.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(box.SelectionStart, box.SelectionEnd), 0, text.Length);
        return text.Remove(start, end - start).Insert(start, input);
    }

    /// <summary>
    /// Starts hotkey capture: pauses the global hotkey and shows the capturing prompt.
    /// </summary>
    private void HotkeyBox_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.SuspendHotkey();
        _savedHotkeyModifiers = vm.HotkeyModifiers;
        _savedHotkeyVirtualKey = vm.HotkeyVirtualKey;
        _hotkeyCommitted = false;
        _isCapturing = true;

        HotkeyBox.Classes.Set("capturing", true);
        HotkeyBox.Text = LocalizationService.Get("Settings.HotkeyCapturing");
    }

    /// <summary>
    /// Ends hotkey capture: resumes the global hotkey and restores the previous value unless a new one was committed.
    /// </summary>
    private void HotkeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        _isCapturing = false;
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.ResumeHotkey();
        if (!_hotkeyCommitted)
        {
            vm.HotkeyModifiers = _savedHotkeyModifiers;
            vm.HotkeyVirtualKey = _savedHotkeyVirtualKey;
        }

        HotkeyBox.Classes.Set("capturing", false);
        HotkeyBox.Text = vm.HotkeyDisplayText;
    }

    /// <summary>
    /// Captures the pressed key combination while the hotkey box has focus.
    /// </summary>
    private void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key;
        if (key is Key.Escape)
        {
            _hotkeyCommitted = false;
            _isCapturing = false;
            RootBorder.Focus();
            return;
        }

        var modifiers = HotkeyKeyMap.ToWin32Modifiers(e.KeyModifiers);

        // Show live preview of current modifier state
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
                or Key.Tab)
        {
            HotkeyBox.Text = modifiers > 0
                ? HotkeyKeyMap.FormatModifiers(modifiers) + "+..."
                : LocalizationService.Get("Settings.HotkeyCapturing");
            return;
        }

        // Require at least one modifier to avoid capturing bare keys, and ignore keys that cannot be a hotkey
        if (modifiers == 0 || !HotkeyKeyMap.TryGetVirtualKey(key, out var vk))
        {
            return;
        }

        if (DataContext is SettingsViewModel vm)
        {
            vm.HotkeyModifiers = modifiers;
            vm.HotkeyVirtualKey = vk;
        }

        // Clear capture state BEFORE moving focus so the outside-click handler no longer
        // treats any subsequent click as an active capture cancellation.
        _hotkeyCommitted = true;
        _isCapturing = false;
        HotkeyBox.Text = HotkeyKeyMap.Format(modifiers, vk);
        RootBorder.Focus();
    }

    /// <summary>
    /// Lets the borderless window be dragged by any part of its background.
    /// </summary>
    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        WindowDrag.TryBegin(this, e);
    }

    /// <summary>
    /// Cancels an active hotkey capture when the user clicks anywhere outside the hotkey box.
    /// </summary>
    private void Window_PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isCapturing && !HotkeyBox.IsPointerOver)
        {
            _hotkeyCommitted = false;
            _isCapturing = false;
            RootBorder.Focus();
        }
    }
}
