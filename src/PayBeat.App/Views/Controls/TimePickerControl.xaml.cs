namespace PayBeat.App.Views.Controls;

/// <summary>
/// Custom time picker control with separate hour and minute text boxes and up/down increment buttons.
/// Exposes a <see cref="SelectedTime"/> dependency property that supports two-way binding to <see cref="TimeOnly"/>.
/// Arrow keys on either text box increment or decrement the respective field.
/// </summary>
public partial class TimePickerControl
{
    /// <summary>
    /// Identifies the <see cref="SelectedTime"/> dependency property.
    /// Registered with <see cref="FrameworkPropertyMetadataOptions.BindsTwoWayByDefault"/>.
    /// </summary>
    public static readonly DependencyProperty SelectedTimeProperty =
        DependencyProperty.Register(nameof(SelectedTime), typeof(TimeOnly), typeof(TimePickerControl),
            new FrameworkPropertyMetadata(TimeOnly.MinValue,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedTimeChanged));

    private bool _isUpdating;

    /// <summary>
    /// Initializes a new instance of <see cref="TimePickerControl"/>, loads the XAML component tree,
    /// and seeds the text boxes with midnight.
    /// </summary>
    public TimePickerControl()
    {
        InitializeComponent();
        UpdateBoxes(TimeOnly.MinValue);
    }

    /// <summary>
    /// Gets or sets the currently selected time. Supports two-way data binding.
    /// </summary>
    public TimeOnly SelectedTime
    {
        get => (TimeOnly)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TimePickerControl)d;
        if (!ctrl._isUpdating)
        {
            ctrl.UpdateBoxes((TimeOnly)e.NewValue);
        }
    }

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

    private void Hour_PreviewKeyDown(object sender, KeyEventArgs e)
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

    private void HourBox_LostFocus(object sender, RoutedEventArgs e)
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

    private void HourDown_Click(object sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddHours(-1);
        UpdateBoxes(SelectedTime);
    }

    private void HourUp_Click(object sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddHours(1);
        UpdateBoxes(SelectedTime);
    }

    private void Minute_PreviewKeyDown(object sender, KeyEventArgs e)
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

    private void MinuteBox_LostFocus(object sender, RoutedEventArgs e)
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

    private void MinuteDown_Click(object sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddMinutes(-1);
        UpdateBoxes(SelectedTime);
    }

    private void MinuteUp_Click(object sender, RoutedEventArgs e)
    {
        SelectedTime = SelectedTime.AddMinutes(1);
        UpdateBoxes(SelectedTime);
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !char.IsAsciiDigit(e.Text[0]);
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        ((TextBox)sender).SelectAll();
    }

    private void UpdateBoxes(TimeOnly time)
    {
        HourBox.Text = time.Hour.ToString("D2");
        MinuteBox.Text = time.Minute.ToString("D2");
    }
}