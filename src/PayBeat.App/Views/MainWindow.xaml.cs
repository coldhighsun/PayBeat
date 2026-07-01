using System.Windows.Media;
using System.Windows.Media.Animation;
using PayBeat.App.Helpers;

namespace PayBeat.App.Views;

/// <summary>
/// Borderless, always-on-top floating widget window. Hosts a <c>ContentControl</c> that switches
/// between <see cref="NormalView"/>, <see cref="CompactView"/>, and <see cref="MiniView"/> based on
/// the active <see cref="Models.DisplayMode"/>. Supports drag-to-move and opacity fade when idle.
/// </summary>
public partial class MainWindow
{
    private readonly ForegroundWatcher _foregroundWatcher;

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
        _foregroundWatcher = new ForegroundWatcher(ReassertTopmost);
        Closed += (_, _) => _foregroundWatcher.Dispose();
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

    /// <summary>
    /// Briefly scales up and dims the window as a click-feedback cue, e.g. when the user activates
    /// it from the tray icon while it is already visible and in the foreground.
    /// </summary>
    public void PlayAttentionAnimation()
    {
        var scale = (ScaleTransform)RenderTransform;
        var scaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.15,
            Duration = TimeSpan.FromMilliseconds(140),
            AutoReverse = true,
            EasingFunction = new QuadraticEase()
        };
        scaleAnimation.Completed += (_, _) =>
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            BeginAnimation(OpacityProperty, null);
            RestoreOpacity();
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        var opacityAnimation = new DoubleAnimation
        {
            From = Opacity,
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(140),
            AutoReverse = true
        };
        BeginAnimation(OpacityProperty, opacityAnimation);
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

    // explorer.exe re-asserts the taskbar's own HWND_TOPMOST position whenever it becomes
    // foreground (e.g. on click), which can push our window behind it even though WPF's
    // Topmost flag is still true. React only to foreground changes rather than polling.
    private void ReassertTopmost()
    {
        if (Topmost)
        {
            TopmostHelper.ForceTopmost(this);
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