using PayBeat.App.Helpers;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PayBeat.App.Views;

/// <summary>
/// Borderless, always-on-top floating widget window. Hosts a <c>ContentControl</c> that switches
/// between <see cref="NormalView"/>, <see cref="CompactView"/>, <see cref="MiniView"/>, and
/// <see cref="FlexView"/> based on the active <see cref="Models.DisplayMode"/>. Supports drag-to-move
/// and opacity fade when idle. In <see cref="Models.DisplayMode.Flex"/>, dragging is still allowed so the
/// user can move the fullscreen widget to another monitor; on mouse release it re-fills whichever
/// monitor it was dropped on.
/// </summary>
public partial class MainWindow
{
    private readonly ForegroundWatcher _foregroundWatcher;
    private Rect? _pendingCenterScreen;

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
        KeyDown += OnKeyDown;
        MouseLeftButtonDown += (_, _) =>
        {
            DragMove();

            // DragMove() blocks until the mouse button is released, so by the time it returns the
            // window may have landed on a different monitor - re-fill it there for Flex mode.
            if (DataContext is ViewModels.MainViewModel vm && vm.DisplayMode == Models.DisplayMode.Flex)
            {
                ApplyFlexBounds();
            }
        };
        _foregroundWatcher = new ForegroundWatcher(ReassertTopmost);
        Closed += (_, _) => _foregroundWatcher.Dispose();
    }

    /// <summary>
    /// Resizes and repositions the window to exactly cover the monitor it is currently on, for
    /// <see cref="Models.DisplayMode.Flex"/>. Switches <see cref="SizeToContent"/> to <c>Manual</c>
    /// since the window normally auto-sizes to its content.
    /// </summary>
    public void ApplyFlexBounds()
    {
        var bounds = ScreenHelper.GetCurrentScreenBounds(this);
        SizeToContent = SizeToContent.Manual;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
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

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ViewModels.MainViewModel { DisplayMode: Models.DisplayMode.Flex } vm)
        {
            // Capture the screen bounds now, while still fullscreen on it, since after switching
            // to Normal the window may briefly report stale ActualWidth/Height until the next layout pass.
            _pendingCenterScreen = ScreenHelper.GetCurrentScreenBounds(this);
            vm.SetNormalModeCommand.Execute(null);
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
        if (DataContext is ViewModels.MainViewModel { DisplayMode: Models.DisplayMode.Flex })
        {
            return;
        }

        if (_pendingCenterScreen is { } screen)
        {
            _pendingCenterScreen = null;
            Left = screen.Left + (screen.Width - ActualWidth) / 2;
            Top = screen.Top + (screen.Height - ActualHeight) / 2;
            return;
        }

        ClampToCurrentScreen();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.Opacity) && !IsMouseOver)
        {
            RestoreOpacity(sender as ViewModels.MainViewModel);
        }

        if (e.PropertyName == nameof(ViewModels.MainViewModel.DisplayMode) && sender is ViewModels.MainViewModel vm)
        {
            if (vm.DisplayMode == Models.DisplayMode.None)
            {
                Hide();
            }
            else if (vm.DisplayMode == Models.DisplayMode.Flex)
            {
                if (!IsVisible)
                {
                    Show();
                }
                ApplyFlexBounds();
            }
            else
            {
                if (SizeToContent == SizeToContent.Manual)
                {
                    RestoreAutoSizing();
                }
                if (!IsVisible)
                {
                    Show();
                    ClampToCurrentScreen();
                }
            }
        }
    }

    // explorer.exe re-asserts the taskbar's own HWND_TOPMOST position whenever it becomes
    // foreground (e.g. on click), which can push our window behind it even though WPF's
    // Topmost flag is still true. React only to foreground changes rather than polling.
    private void ReassertTopmost()
    {
        if (!Topmost)
        {
            return;
        }

        // Don't fight Settings/About for the topmost band - they are shown as owned, topmost
        // windows themselves (see MainViewModel.ApplyTopmostIfNeeded) and would otherwise get
        // buried again the moment they take focus and trigger this foreground-change callback.
        foreach (Window w in Application.Current.Windows)
        {
            if (w is SettingsWindow or AboutWindow)
            {
                return;
            }
        }

        TopmostHelper.ForceTopmost(this);
    }

    private void RestoreAutoSizing()
    {
        ClearValue(WidthProperty);
        ClearValue(HeightProperty);
        SizeToContent = SizeToContent.WidthAndHeight;
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