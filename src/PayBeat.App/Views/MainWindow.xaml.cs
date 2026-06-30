using PayBeat.App.Helpers;

namespace PayBeat.App.Views;

/// <summary>
/// Borderless, always-on-top floating widget window. Hosts a <c>ContentControl</c> that switches
/// between <see cref="NormalView"/>, <see cref="CompactView"/>, and <see cref="MiniView"/> based on
/// the active <see cref="Models.DisplayMode"/>. Supports drag-to-move and opacity fade when idle.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Initializes the window and wires up mouse and data-context event handlers.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        DataContextChanged += OnDataContextChanged;
        MouseEnter += (_, _) => Opacity = 1.0;
        MouseLeave += (_, _) => RestoreOpacity();
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    /// <summary>
    /// Clamps the window position so it stays within the bounds of the monitor it is currently on.
    /// </summary>
    public void ClampToCurrentScreen()
    {
        ClampToWorkArea(ScreenHelper.GetCurrentScreenBounds(this));
    }

    /// <summary>
    /// Clamps <see cref="Window.Left"/> and <see cref="Window.Top"/> so the window stays within
    /// <paramref name="workArea"/>. No-ops when the window has not yet been measured.
    /// </summary>
    /// <param name="workArea">Screen bounds in WPF logical units.</param>
    public void ClampToWorkArea(Rect workArea)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Left = Math.Clamp(Left, workArea.Left, workArea.Right - ActualWidth);
        Top = Math.Clamp(Top, workArea.Top, workArea.Bottom - ActualHeight);
    }

    private void ContentControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && DataContext is ViewModels.MainViewModel vm)
        {
            vm.OpenSettingsCommand.Execute(null);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewModels.MainViewModel old)
        {
            old.PropertyChanged -= OnVmPropertyChanged;
        }

        if (e.NewValue is ViewModels.MainViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            RestoreOpacity(vm);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 8;
        Top = workArea.Bottom - ActualHeight - 8;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClampToCurrentScreen();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.Opacity) && !IsMouseOver)
        {
            RestoreOpacity(sender as ViewModels.MainViewModel);
        }
    }

    private void RestoreOpacity(ViewModels.MainViewModel? vm = null)
    {
        vm ??= DataContext as ViewModels.MainViewModel;
        if (vm != null)
        {
            Opacity = vm.Opacity;
        }
    }
}