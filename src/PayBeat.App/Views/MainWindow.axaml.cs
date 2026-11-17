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
    /// Delay between the pointer leaving the window and the window sliding away at a docked edge.
    /// </summary>
    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Distance, in device-independent pixels, within which a dropped window counts as docked to an edge.
    /// </summary>
    private const double DockThreshold = 12;

    /// <summary>
    /// Thickness, in device-independent pixels, of the sliver that stays visible while collapsed.
    /// </summary>
    private const double CollapsedStrip = 4;

    /// <summary>
    /// How often a docked window checks where the pointer is.
    /// </summary>
    private static readonly TimeSpan AutoHidePollInterval = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Polls the pointer while the window is docked: collapses it once the pointer stayed away for
    /// <see cref="AutoHideDelay"/> and brings it out when the pointer reaches the visible sliver. Polling is used
    /// because enter/leave events are not delivered reliably after a drag with pointer capture.
    /// </summary>
    private readonly DispatcherTimer _autoHideTimer;

    /// <summary>
    /// When the pointer was first seen away from the shown window, or <see langword="null"/> while it is on it.
    /// </summary>
    private DateTimeOffset? _pointerAwaySince;

    /// <summary>
    /// Whether the window was brought out from the tray icon and has to stay out until the pointer has been on it
    /// and left again, because the pointer is on the tray at that moment.
    /// </summary>
    private bool _awaitPointerEnter;

    /// <summary>
    /// How long the list of screens has to stay unchanged before the window is checked against it.
    /// </summary>
    private static readonly TimeSpan ScreensSettleDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Whether the screen-change handler is already attached.
    /// </summary>
    private bool _screensWatched;

    /// <summary>
    /// The pending wait for the screen list to settle, or <see langword="null"/> when none is pending.
    /// </summary>
    private IDisposable? _screensSettle;

    /// <summary>
    /// Edge the window is currently docked to, or <see cref="DockEdge.None"/>.
    /// </summary>
    private DockEdge _dockEdge;

    /// <summary>
    /// Fully visible position of the window at <see cref="_dockEdge"/>.
    /// </summary>
    private PixelPoint _dockedPosition;

    /// <summary>
    /// Whether the window is currently slid off-screen leaving only the sliver visible.
    /// </summary>
    private bool _isCollapsed;

    /// <summary>
    /// Whether the context menu is open, which keeps the window expanded.
    /// </summary>
    private bool _isMenuOpen;

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
        _autoHideTimer = new DispatcherTimer { Interval = AutoHidePollInterval };
        _autoHideTimer.Tick += (_, _) => OnAutoHideTick();
        PointerEntered += (_, _) =>
        {
            Opacity = 1.0;
            Expand();
        };
        PointerExited += (_, _) => RestoreOpacity();
        if (ContextMenu is { } menu)
        {
            menu.Opened += (_, _) => _isMenuOpen = true;
            menu.Closed += (_, _) =>
            {
                _isMenuOpen = false;
                _pointerAwaySince = null;
            };
        }

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
    public WindowPosition CapturePosition()
    {
        // While collapsed the real position is mostly off-screen; persist the visible docked one.
        var position = _isCollapsed ? _dockedPosition : Position;
        return new(position.X, position.Y, ScreenHelper.GetCurrentScreenName(this));
    }

    /// <summary>
    /// Docks the window to a nearby screen edge when auto-hide is enabled and collapses it once the
    /// pointer is away. Clears any docked state when auto-hide does not apply.
    /// </summary>
    /// <param name="dock">
    /// Whether to look for an edge the window rests at. <see langword="false"/> keeps a window at its default spot
    /// from being docked.
    /// </param>
    /// <param name="collapseNow">
    /// Whether to hide a docked window at once instead of after the usual delay, as at startup.
    /// </param>
    public void ApplyAutoHide(bool dock = true, bool collapseNow = false)
    {
        if (!IsAutoHideActive)
        {
            Expand();
            _dockEdge = DockEdge.None;
            UpdateAutoHideWatcher();
            return;
        }

        if (dock)
        {
            TryDock();
        }

        UpdateAutoHideWatcher();
        if (collapseNow)
        {
            Collapse();
        }
    }

    /// <summary>
    /// Handles a click on the tray icon: a window that is hidden, either collapsed at a screen edge or not shown at
    /// all, is brought out; a window that is already in sight flashes instead.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a hidden window was brought out; <see langword="false"/> when the window was
    /// already visible and only flashed.
    /// </returns>
    public bool ActivateFromTray()
    {
        var wasHidden = !IsVisible || _isCollapsed;
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Expand();
        if (wasHidden && _dockEdge != DockEdge.None)
        {
            _awaitPointerEnter = true;
        }

        Activate();
        if (!wasHidden)
        {
            PlayAttentionAnimation();
        }

        return wasHidden;
    }

    /// <summary>
    /// Places the window at the default spot: the bottom-right corner of the primary screen's work area.
    /// </summary>
    public void PlaceAtDefault()
    {
        if (Screens.Primary is not { } screen)
        {
            return;
        }

        _isCollapsed = false;
        _dockEdge = DockEdge.None;
        Position = WindowPlacement.BottomRight(ScreenHelper.GetPixelSize(this), screen.WorkingArea, (int)(8 * screen.Scaling));
        UpdateAutoHideWatcher();
    }

    /// <summary>
    /// Moves the window to the default spot when no screen shows it any more, for example after a monitor was
    /// unplugged while the window was docked or hidden there.
    /// </summary>
    private void EnsureOnScreen()
    {
        if (DataContext is MainViewModel { DisplayMode: DisplayMode.Flex or DisplayMode.None })
        {
            return;
        }

        var size = ScreenHelper.GetPixelSize(this);
        var visible = new PixelRect(_isCollapsed ? _dockedPosition : Position, size);
        if (size.Width <= 0 || Screens.All.Any(s => s.Bounds.Intersects(visible)))
        {
            return;
        }

        PlaceAtDefault();
    }

    /// <summary>
    /// Gets a value indicating whether auto-hide is enabled and the current display mode supports it.
    /// </summary>
    private bool IsAutoHideActive =>
        DataContext is MainViewModel { AutoHideAtEdge: true, DisplayMode: DisplayMode.Normal or DisplayMode.Mini };

    /// <summary>
    /// Detects the edge the window sits against, snaps flush to it and remembers the docked position.
    /// Edges shared with another monitor are ignored, since the collapsed window would show up there.
    /// </summary>
    private void TryDock()
    {
        _dockEdge = DockEdge.None;
        if (_isCollapsed || ScreenHelper.GetCurrentScreen(this) is not { } screen)
        {
            return;
        }

        var size = ScreenHelper.GetPixelSize(this);
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var area = screen.Bounds;
        var edge = WindowPlacement.DetectDockEdge(Position, size, area, (int)(DockThreshold * screen.Scaling));
        if (edge == DockEdge.None)
        {
            return;
        }

        var docked = WindowPlacement.DockedPosition(edge, Position, size, area);
        var probe = WindowPlacement.HiddenPosition(edge, docked, size, area, 1);
        var center = new PixelPoint(probe.X + (size.Width / 2), probe.Y + (size.Height / 2));

        // PixelRect.Contains is inclusive of the right and bottom edges, so probe one pixel further out.
        var outside = edge switch
        {
            DockEdge.Left => new PixelPoint(area.X - 1, center.Y),
            DockEdge.Right => new PixelPoint(area.Right + 1, center.Y),
            DockEdge.Top => new PixelPoint(center.X, area.Y - 1),
            _ => new PixelPoint(center.X, area.Bottom + 1),
        };
        if (Screens.All.Any(s => s.Bounds.Contains(outside)))
        {
            return;
        }

        _dockEdge = edge;
        _dockedPosition = docked;
        Position = docked;
    }

    /// <summary>
    /// Runs the pointer poll only while the window is docked and auto-hide applies.
    /// </summary>
    private void UpdateAutoHideWatcher()
    {
        _pointerAwaySince = null;
        _awaitPointerEnter = false;
        if (_dockEdge != DockEdge.None && IsAutoHideActive)
        {
            _autoHideTimer.Start();
        }
        else
        {
            _autoHideTimer.Stop();
        }
    }

    /// <summary>
    /// Waits for the list of screens to stop changing, then makes sure the window is still on one of them. The system
    /// can report several incomplete lists while a monitor is plugged in or out, so acting on the first one is unsafe.
    /// </summary>
    private void OnScreensChanged(object? sender, EventArgs e)
    {
        _screensSettle?.Dispose();
        _screensSettle = DispatcherTimer.RunOnce(
            () =>
            {
                _screensSettle = null;
                EnsureOnScreen();
            },
            ScreensSettleDelay);
    }

    /// <summary>
    /// Brings a collapsed window out when the pointer reaches its sliver, and collapses a shown one after the
    /// pointer stayed away for <see cref="AutoHideDelay"/>.
    /// </summary>
    private void OnAutoHideTick()
    {
        if (_dockEdge == DockEdge.None || !IsAutoHideActive)
        {
            _autoHideTimer.Stop();
            return;
        }

        var onWindow = IsPointerOnWindow();
        if (_isCollapsed)
        {
            if (onWindow)
            {
                Expand();
            }

            return;
        }

        if (_awaitPointerEnter)
        {
            // Brought out from the tray: stay out until the pointer has visited the window.
            _awaitPointerEnter = !onWindow;
            _pointerAwaySince = null;
            return;
        }

        if (onWindow || _isMenuOpen || IsSettingsOrAboutOpen())
        {
            _pointerAwaySince = null;
            return;
        }

        _pointerAwaySince ??= DateTimeOffset.UtcNow;
        if (DateTimeOffset.UtcNow - _pointerAwaySince >= AutoHideDelay)
        {
            Collapse();
        }
    }

    /// <summary>
    /// Determines whether the pointer is on the window. Where the system reports the pointer position it decides,
    /// because <see cref="InputElement.IsPointerOver"/> can stay stale after a drag; elsewhere the event state is used.
    /// </summary>
    /// <returns><see langword="true"/> when the pointer is within the window bounds.</returns>
    private bool IsPointerOnWindow()
    {
        if (!PointerProbe.TryGetPosition(out var pointer))
        {
            return IsPointerOver;
        }

        var size = ScreenHelper.GetPixelSize(this);
        var bounds = new PixelRect(Position, size);
        return new PixelRect(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2).Contains(pointer);
    }

    /// <summary>
    /// Slides the docked window off-screen leaving only a thin sliver, unless the user is interacting with it.
    /// </summary>
    internal void Collapse()
    {
        if (_isCollapsed || _dockEdge == DockEdge.None || !IsAutoHideActive || IsPointerOnWindow() || _isMenuOpen || IsSettingsOrAboutOpen())
        {
            return;
        }

        if (ScreenHelper.GetCurrentScreen(this) is not { } screen)
        {
            return;
        }

        var size = ScreenHelper.GetPixelSize(this);
        _isCollapsed = true;
        Position = WindowPlacement.HiddenPosition(_dockEdge, _dockedPosition, size, screen.Bounds, Math.Max(1, (int)(CollapsedStrip * screen.Scaling)));
    }

    /// <summary>
    /// Cancels a pending collapse and brings a collapsed window back to its docked position.
    /// </summary>
    private void Expand()
    {
        _pointerAwaySince = null;
        if (!_isCollapsed)
        {
            return;
        }

        _isCollapsed = false;
        Position = _dockedPosition;
    }

    /// <summary>
    /// Returns whether the Settings or About window is currently open.
    /// </summary>
    private static bool IsSettingsOrAboutOpen() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
        && desktop.Windows.Any(w => w is SettingsWindow or AboutWindow);

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
        if (_isCollapsed)
        {
            return;
        }

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
            return;
        }

        if (moved)
        {
            ApplyAutoHide();
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
        // Re-shown (e.g. via the hotkey) while collapsed: bring it back into view.
        Expand();

        if (!_screensWatched && Screens is { } screens)
        {
            _screensWatched = true;
            screens.Changed += OnScreensChanged;
        }

        if (_initialPlacementDone || IsRestoringStartupPosition)
        {
            return;
        }

        _initialPlacementDone = true;
        PlaceAtDefault();
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

        if (e.PropertyName is nameof(MainViewModel.AutoHideAtEdge) or nameof(MainViewModel.DisplayMode))
        {
            // Restore a collapsed window first so mode changes and sizing act on the visible position.
            Expand();
            _dockEdge = DockEdge.None;
            UpdateAutoHideWatcher();
        }

        if (e.PropertyName == nameof(MainViewModel.AutoHideAtEdge))
        {
            ApplyAutoHide();
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
        if (IsSettingsOrAboutOpen())
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
