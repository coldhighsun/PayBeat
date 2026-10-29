using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PayBeat.App.Views.Controls;

/// <summary>
/// Custom time picker control with separate hour and minute text boxes and up/down increment buttons.
/// Exposes a <see cref="SelectedTime"/> styled property that supports two-way binding to <see cref="TimeOnly"/>.
/// Arrow keys on either text box increment or decrement the respective field.
/// </summary>
public partial class TimePickerControl : UserControl
{
    /// <summary>
    /// Identifies the <see cref="SelectedTime"/> styled property. Binds two-way by default.
    /// </summary>
    public static readonly StyledProperty<TimeOnly> SelectedTimeProperty =
        AvaloniaProperty.Register<TimePickerControl, TimeOnly>(nameof(SelectedTime), TimeOnly.MinValue, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Whether the control itself is writing <see cref="SelectedTime"/>, so the change callback does not rewrite the boxes.
    /// </summary>
    private bool _isUpdating;

    /// <summary>
    /// Initializes a new instance of <see cref="TimePickerControl"/>, loads the XAML component tree,
    /// and seeds the text boxes with midnight.
    /// </summary>
    public TimePickerControl()
    {
        InitializeComponent();
        UpdateBoxes(TimeOnly.MinValue);

        HourBox.AddHandler(TextInputEvent, NumericOnly_TextInput, RoutingStrategies.Tunnel);
        MinuteBox.AddHandler(TextInputEvent, NumericOnly_TextInput, RoutingStrategies.Tunnel);
        HourBox.AddHandler(KeyDownEvent, Hour_KeyDown, RoutingStrategies.Tunnel);
        MinuteBox.AddHandler(KeyDownEvent, Minute_KeyDown, RoutingStrategies.Tunnel);
        HourBox.GotFocus += TextBox_GotFocus;
        MinuteBox.GotFocus += TextBox_GotFocus;
        HourBox.LostFocus += HourBox_LostFocus;
        MinuteBox.LostFocus += MinuteBox_LostFocus;
    }

    /// <summary>
    /// Gets or sets the currently selected time. Supports two-way data binding.
    /// </summary>
    public TimeOnly SelectedTime
    {
        get => GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedTimeProperty && !_isUpdating && change.NewValue is TimeOnly time)
        {
            UpdateBoxes(time);
        }
    }

    /// <summary>
    /// Commits the typed hour and minute to <see cref="SelectedTime"/> when both are valid.
    /// </summary>
    private void CommitFromBoxes()
    {
        if (int.TryParse(HourBox.Text, out var h) && int.TryParse(MinuteBox.Text, out var m)
            && h is >= 0 and <= 23 && m is >= 0 and <= 59)
        {
            _isUpdating = true;
            SelectedTime = new TimeOnly(h, m);
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Handles Up/Down on the hour box.
    /// </summary>
    private void Hour_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            HourUp_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            HourDown_Click(sender, e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Normalizes and commits the hour when the box loses focus.
    /// </summary>
    private void HourBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(HourBox.Text, out var h))
        {
            HourBox.Text = Math.Clamp(h, 0, 23).ToString("D2");
        }
        else
        {
            HourBox.Text = SelectedTime.Hour.ToString("D2");
        }

        CommitFromBoxes();
    }

    /// <summary>
    /// Decrements the hour.
    /// </summary>
    private void HourDown_Click(object? sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddHours(-1);
        UpdateBoxes(SelectedTime);
    }

    /// <summary>
    /// Increments the hour.
    /// </summary>
    private void HourUp_Click(object? sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddHours(1);
        UpdateBoxes(SelectedTime);
    }

    /// <summary>
    /// Handles Up/Down on the minute box.
    /// </summary>
    private void Minute_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            MinuteUp_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            MinuteDown_Click(sender, e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Normalizes and commits the minute when the box loses focus.
    /// </summary>
    private void MinuteBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(MinuteBox.Text, out var m))
        {
            MinuteBox.Text = Math.Clamp(m, 0, 59).ToString("D2");
        }
        else
        {
            MinuteBox.Text = SelectedTime.Minute.ToString("D2");
        }

        CommitFromBoxes();
    }

    /// <summary>
    /// Decrements the minute.
    /// </summary>
    private void MinuteDown_Click(object? sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddMinutes(-1);
        UpdateBoxes(SelectedTime);
    }

    /// <summary>
    /// Increments the minute.
    /// </summary>
    private void MinuteUp_Click(object? sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddMinutes(1);
        UpdateBoxes(SelectedTime);
    }

    /// <summary>
    /// Rejects any typed character that is not an ASCII digit.
    /// </summary>
    private void NumericOnly_TextInput(object? sender, TextInputEventArgs e)
    {
        e.Handled = string.IsNullOrEmpty(e.Text) || !char.IsAsciiDigit(e.Text[0]);
    }

    /// <summary>
    /// Selects the whole content so typing replaces it.
    /// </summary>
    private void TextBox_GotFocus(object? sender, RoutedEventArgs e)
    {
        ((TextBox)sender!).SelectAll();
    }

    /// <summary>
    /// Writes the hour and minute of <paramref name="time"/> into the text boxes.
    /// </summary>
    private void UpdateBoxes(TimeOnly time)
    {
        HourBox.Text = time.Hour.ToString("D2");
        MinuteBox.Text = time.Minute.ToString("D2");
    }
}
