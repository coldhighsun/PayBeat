using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using PayBeat.App.Helpers;
using PayBeat.App.Models;
using PayBeat.App.Platform;
using PayBeat.App.ViewModels;

namespace PayBeat.App.Views;

/// <summary>
/// Borderless, always-on-top floating widget window. Hosts <see cref="NormalView"/>, <see cref="MiniView"/>
/// and <see cref="FlexView"/> and shows the one matching the active <see cref="DisplayMode"/>.
/// Supports drag-to-move and opacity fade when idle. In <see cref="DisplayMode.Flex"/>, dragging is still
/// allowed so the user can move the fullscreen widget to another monitor; once the drag ends it re-fills
/// whichever monitor it was dropped on.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Re-asserts the topmost z-order on platforms that need it.
    /// </summary>
    private readonly ITopmostEnforcer _topmostEnforcer;

    /// <summary>
    /// Bounds of the screen the window was fullscreen on when leaving Flex mode, used to center it there.
    /// </summary>
    private PixelRect? _pendingCenterScreen;

    /// <summary>
    /// Whether the first-show default placement already ran (or was skipped because a position was restored).
    /// </summary>
    private bool _initialPlacementDone;

    /// <summary>
    /// Initializes the window with the topmost enforcer of the current platform.
    /// </summary>
    public MainWindow()
        : this(PlatformServices.CreateTopmostEnforcer())
    {
    }

    /// <summary>
    /// Initializes the window and wires up pointer, keyboard and data-context event handlers.
    /// </summary>
    /// <param name="topmostEnforcer">Platform helper that keeps the window above others.</param>
    public MainWindow(ITopmostEnforcer topmostEnforcer)
    {
        _topmostEnforcer = topmostEnforcer;
        InitializeComponent();

        Opened += OnOpened;
        SizeChanged += OnSizeChanged;
        PointerEntered += (_, _) => Opacity = 1.0;
        PointerExited += (_, _) => RestoreOpacity();
        KeyDown += OnKeyDown;
        PointerPressed += OnPointerPressed;
        Closing += (_, _) => LastKnownPosition = CapturePosition();
        Closed += (_, _) => _topmostEnforcer.Dispose();

        _topmostEnforcer.Watch(ReassertTopmost);
    }

    /// <summary>
    /// Gets or sets a value indicating whether startup restore is in progress so default placement and
    /// clamping handlers do not override the position applied by <c>App</c> after first render.
    /// </summary>
    public bool IsRestoringStartupPosition
    {
        get; set;
    }

    /// <summary>
    /// Gets the window's screen name and position captured while it was still open, so the app can
    /// persist them during shutdown when the window can no longer be queried.
    /// </summary>
    public WindowPosition? LastKnownPosition
    {
        get; private set;
    }

    /// <summary>
    /// Returns the window's current position and screen as a persistable value.
    /// </summary>
    /// <returns>The current position.</returns>
    public WindowPosition CapturePosition() =>
        new(Position.X, Position.Y, ScreenHelper.GetCurrentScreenName(this));

    /// <summary>
    /// Sets the position before the window is first shown and suppresses the default placement.
    /// </summary>
    /// <param name="position">Top-left corner in device pixels.</param>
    public void RestorePosition(PixelPoint position)
    {
        _initialPlacementDone = true;
        Position = position;
    }

    /// <summary>
    /// Resizes and repositions the window to exactly cover <paramref name="screen"/>, or the
    /// screen it is currently on if not specified, for <see cref="DisplayMode.Flex"/>.
    /// Switches <see cref="Window.SizeToContent"/> to <c>Manual</c> since the window normally auto-sizes to its content.
    /// </summary>
    /// <param name="screen">The screen to fill, or <see langword="null"/> for the current screen.</param>
    public void ApplyFlexBounds(Screen? screen = null)
    {
        var target = screen ?? ScreenHelper.GetCurrentScreen(this);
        if (target is null)
        {
            return;
        }

        var bounds = target.Bounds;
        SizeToContent = SizeToContent.Manual;
        Position = bounds.Position;
        ApplySize(bounds);

        // Moving between monitors with different scaling can change RenderScaling after the move,
        // so correct the size once the new scaling is in effect.
        Dispatcher.UIThread.Post(() => ApplySize(bounds));
    }

    /// <summary>
    /// Clamps the window position so it stays within the bounds of the monitor it is currently on.
    /// </summary>
    public void ClampToCurrentScreen()
    {
        if (ScreenHelper.GetCurrentScreen(this) is { } screen)
        {
            ClampToWorkArea(screen.Bounds);
        }
    }

    /// <summary>
    /// Clamps <see cref="Window.Position"/> so the window stays within <paramref name="area"/>.
    /// No-ops when the window has not yet been measured.
    /// </summary>
    /// <param name="area">Screen bounds in device pixels.</param>
    public void ClampToWorkArea(PixelRect area)
    {
        var size = ScreenHelper.GetPixelSize(this);
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        Position = WindowPlacement.Clamp(Position, size, area);
    }

    /// <summary>
    /// Briefly scales up and dims the window as a click-feedback cue, e.g. when the user activates
    /// it from the tray icon while it is already visible and in the foreground.
    /// </summary>
    public void PlayAttentionAnimation()
    {
        var duration = TimeSpan.FromMilliseconds(280);
        var scaleAnimation = new Animation
        {
            Duration = duration,
            Easing = new QuadraticEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.0), new Setter(ScaleTransform.ScaleYProperty, 1.0) } },
                new KeyFrame { Cue = new Cue(0.5d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.15), new Setter(ScaleTransform.ScaleYProperty, 1.15) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.0), new Setter(ScaleTransform.ScaleYProperty, 1.0) } },
            },
        };
        var opacityAnimation = new Animation
        {
            Duration = duration,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, Opacity) } },
                new KeyFrame { Cue = new Cue(0.5d), Setters = { new Setter(OpacityProperty, 0.6) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, Opacity) } },
            },
        };

        _ = RunAttentionAsync(scaleAnimation, opacityAnimation);
    }

    /// <summary>
    /// Runs the attention animations and restores the idle opacity afterwards.
    /// </summary>
    private async Task RunAttentionAsync(Animation scaleAnimation, Animation opacityAnimation)
    {
        await Task.WhenAll(scaleAnimation.RunAsync((ScaleTransform)RootHost.RenderTransform!), opacityAnimation.RunAsync(this));
        RestoreOpacity();
    }

    /// <summary>
    /// Sets the window size from pixel bounds using the window's current render scaling.
    /// </summary>
    private void ApplySize(PixelRect bounds)
    {
        var scale = RenderScaling;
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;
    }

    /// <summary>
    /// Opens settings on double click, otherwise starts dragging the window.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2 && DataContext is MainViewModel doubleClicked)
        {
            doubleClicked.OpenSettingsCommand.Execute(null);
            return;
        }

        WindowDrag.TryBegin(this, e, OnDragEnded);
    }

    /// <summary>
    /// Re-fills the monitor the window was dropped on in Flex mode, but only if it was actually dragged.
    /// </summary>
    /// <param name="moved">Whether the window position changed during the drag.</param>
    private void OnDragEnded(bool moved)
    {
        if (moved && DataContext is MainViewModel { DisplayMode: DisplayMode.Flex })
        {
            ApplyFlexBounds();
        }
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.PropertyChanged += OnVmPropertyChanged;
            RestoreOpacity(vm);
        }
    }

    /// <summary>
    /// Leaves Flex mode when Escape is pressed.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainViewModel { DisplayMode: DisplayMode.Flex } vm)
        {
            // Capture the screen bounds now, while still fullscreen on it, since after switching
            // to Normal the window may briefly report stale sizes until the next layout pass.
            _pendingCenterScreen = ScreenHelper.GetCurrentScreen(this)?.Bounds;
            vm.SetNormalModeCommand.Execute(null);
        }
    }

    /// <summary>
    /// Places the window in the bottom-right corner of the primary work area on its very first show
    /// when no saved position was restored.
    /// </summary>
    private void OnOpened(object? sender, EventArgs e)
    {
        if (_initialPlacementDone || IsRestoringStartupPosition)
        {
            return;
        }

        _initialPlacementDone = true;
        if (Screens.Primary is { } screen)
        {
            Position = WindowPlacement.BottomRight(ScreenHelper.GetPixelSize(this), screen.WorkingArea, (int)(8 * screen.Scaling));
        }
    }

    /// <summary>
    /// Keeps the widget on screen when its size changes, or centers it on the screen it just left
    /// Flex mode on.
    /// </summary>
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (IsRestoringStartupPosition)
        {
            return;
        }

        if (DataContext is MainViewModel { DisplayMode: DisplayMode.Flex })
        {
            return;
        }

        if (_pendingCenterScreen is { } screen)
        {
            _pendingCenterScreen = null;
            Position = WindowPlacement.Center(ScreenHelper.GetPixelSize(this), screen);
            return;
        }

        ClampToCurrentScreen();
    }

    /// <summary>
    /// Applies opacity, visibility and sizing changes caused by view model property changes.
    /// </summary>
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Opacity) && !IsPointerOver)
        {
            RestoreOpacity(sender as MainViewModel);
        }

        if (e.PropertyName == nameof(MainViewModel.DisplayMode) && sender is MainViewModel vm)
        {
            if (vm.DisplayMode == DisplayMode.None)
            {
                Hide();
            }
            else if (vm.DisplayMode == DisplayMode.Flex)
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

    /// <summary>
    /// Re-applies the topmost band after another application took the foreground. On Windows,
    /// explorer.exe re-asserts the taskbar's own topmost position whenever it becomes foreground
    /// (e.g. on click), which can push this window behind it even though <see cref="WindowBase.Topmost"/>
    /// is still true.
    /// </summary>
    private void ReassertTopmost()
    {
        if (!Topmost)
        {
            return;
        }

        // Don't fight Settings/About for the topmost band - they are shown as owned, topmost
        // windows themselves and would otherwise get buried again the moment they take focus and
        // trigger this foreground-change callback.
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.Windows.Any(w => w is SettingsWindow or AboutWindow))
        {
            return;
        }

        _topmostEnforcer.ForceTopmost(this);
    }

    /// <summary>
    /// Switches back from manual (Flex) sizing to sizing the window to its content.
    /// </summary>
    private void RestoreAutoSizing()
    {
        ClearValue(WidthProperty);
        ClearValue(HeightProperty);
        SizeToContent = SizeToContent.WidthAndHeight;
    }

    /// <summary>
    /// Sets the window opacity to the configured idle value.
    /// </summary>
    private void RestoreOpacity(MainViewModel? vm = null)
    {
        vm ??= DataContext as MainViewModel;
        if (vm != null)
        {
            Opacity = vm.Opacity;
        }
    }
}
