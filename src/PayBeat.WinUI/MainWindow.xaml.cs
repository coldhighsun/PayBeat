using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using PayBeat.Core.Models;
using PayBeat.WinUI.Helpers;
using PayBeat.WinUI.Services;
using PayBeat.WinUI.ViewModels;
using Windows.Graphics;

namespace PayBeat.WinUI;

/// <summary>
/// Standard-chrome, optionally always-on-top widget window. Reacts to <see cref="MainViewModel"/>
/// property changes (mirroring the WPF build's <c>OnVmPropertyChanged</c>) to drive content-based
/// sizing, opacity fade on hover, Escape to exit Flex, and topmost re-assertion against
/// explorer.exe stealing z-order. Hosts <see cref="Views.NormalView"/>/<see cref="Views.MiniView"/>/<see cref="Views.FlexView"/>,
/// switched via direct visibility toggling rather than a WPF-style DataTemplate.
/// </summary>
public sealed partial class MainWindow
{
    private readonly AcrylicBackdropHelper _backdrop;
    private readonly ForegroundWatcher _foregroundWatcher;
    private readonly OverlappedPresenter _presenter;
    private bool _attentionAnimationRunning;
    private DisplayMode _displayMode = DisplayMode.Normal;
    private RectInt32? _pendingCenterScreen;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;

        NormalContent.DataContext = viewModel;
        MiniContent.DataContext = viewModel;
        FlexContent.DataContext = viewModel;

        _backdrop = new AcrylicBackdropHelper(this, viewModel.Opacity);
        NormalContent.SetBackgroundOpacity(viewModel.Opacity);
        MiniContent.SetBackgroundOpacity(viewModel.Opacity);

        _presenter = (OverlappedPresenter)AppWindow.Presenter;
        _presenter.IsAlwaysOnTop = viewModel.AlwaysOnTop;
        _presenter.SetBorderAndTitleBar(true, false);
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;

        _foregroundWatcher = new(ReassertTopmost);

        SetupEscapeAccelerator();
        WindowDragHelper.Attach(RootGrid, AppWindow);
        RootGrid.Loaded += (_, _) => ResizeToContent();

        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        SetDisplayModeChrome(viewModel.DisplayMode);
    }

    public string AboutLabel => LocalizationService.Instance["Menu.About"];

    /// <summary>
    /// The currently active display mode, exposed for the tray menu's checkmarks.
    /// </summary>
    public DisplayMode CurrentDisplayMode => _displayMode;

    public string DisplayModeLabel => LocalizationService.Instance["Menu.DisplayMode"];

    public string ExitLabel => LocalizationService.Instance["Menu.Exit"];

    public string FlexLabel => LocalizationService.Instance["Menu.Flex"];

    public string MiniLabel => LocalizationService.Instance["Menu.Mini"];

    public string NoneLabel => LocalizationService.Instance["Menu.None"];

    public string NormalLabel => LocalizationService.Instance["Menu.Normal"];

    public string SettingsLabel => LocalizationService.Instance["Menu.Settings"];

    /// <summary>
    /// The view model driving earnings/display-mode state; owned by <c>App</c>.
    /// </summary>
    public MainViewModel ViewModel
    {
        get;
    }

    /// <summary>
    /// The window's HWND, exposed for services (hotkey registration) that need it before/instead
    /// of going through <c>WindowNative</c> again.
    /// </summary>
    internal nint Handle => Hwnd;

    /// <summary>
    /// Shows/activates the window (see <see cref="ShowAndActivate"/>) and briefly scales it up and
    /// dims it as a click-feedback cue, mirroring the WPF build's <c>PlayAttentionAnimation</c>.
    /// Used by the tray icon when the widget is clicked while already visible and in the foreground.
    /// </summary>
    public void ActivateWithAttention()
    {
        ShowAndActivate();
        PlayAttentionAnimation();
    }

    /// <summary>
    /// Switches the widget to <paramref name="mode"/> via the view model, persisting the change.
    /// Used by the tray icon, which has no direct access to <see cref="ViewModel"/>'s commands.
    /// </summary>
    public void SetDisplayMode(DisplayMode mode)
    {
        switch (mode)
        {
            case DisplayMode.Normal:
                ViewModel.SetNormalModeCommand.Execute(null);
                break;

            case DisplayMode.Mini:
                ViewModel.SetMiniModeCommand.Execute(null);
                break;

            case DisplayMode.Flex:
                ViewModel.SetFlexModeCommand.Execute(null);
                break;

            case DisplayMode.None:
                ViewModel.SetNoneModeCommand.Execute(null);
                break;
        }
    }

    /// <summary>Shows the window (if not hidden by <see cref="DisplayMode.None"/>) and brings it to the foreground.</summary>
    public void ShowAndActivate()
    {
        if (_displayMode == DisplayMode.None)
        {
            return;
        }

        if (!AppWindow.IsVisible)
        {
            AppWindow.Show();
        }
        Activate();
    }

    /// <summary>Shows or hides the window, mirroring the WPF build's hotkey toggle behavior.</summary>
    public void ToggleVisibility()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
        else if (_displayMode != DisplayMode.None)
        {
            AppWindow.Show();
        }
    }

    /// <summary>
    /// Applies <paramref name="opacity"/> to the backdrop's tint/luminosity and to each view's
    /// background fill (see <see cref="Views.NormalView.SetBackgroundOpacity"/>). Both are needed:
    /// the backdrop alone is invisible behind each view's opaque background border.
    /// </summary>
    private void ApplyOpacity(double opacity)
    {
        _backdrop.SetOpacity(opacity);
        NormalContent.SetBackgroundOpacity(opacity);
        MiniContent.SetBackgroundOpacity(opacity);
    }

    private void Content_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
                ViewModel.OpenSettingsCommand.Execute(null);

    private void FlexMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel.SetFlexModeCommand.Execute(null);

    private void MiniMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel.SetMiniModeCommand.Execute(null);

    private void NoneMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel.SetNoneModeCommand.Execute(null);

    private void NormalMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel.SetNormalModeCommand.Execute(null);

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Bindings.Update();
        ResizeToContent();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.DisplayMode):
                SetDisplayModeChrome(ViewModel.DisplayMode);
                break;

            case nameof(MainViewModel.AlwaysOnTop):
                _presenter.IsAlwaysOnTop = ViewModel.AlwaysOnTop;
                break;

            case nameof(MainViewModel.Opacity):
                ApplyOpacity(ViewModel.Opacity);
                break;

            case nameof(MainViewModel.EarnedFormatted):
                ResizeToContent();
                break;
        }
    }

    /// <summary>
    /// Briefly scales up and dims the window as a click-feedback cue. Unlike WPF's borderless,
    /// transparent top-level window - which lets <c>RenderTransform</c> overflow past the window
    /// rect without clipping - a WinUI3 <c>AppWindow</c> is a real HWND that clips its content to
    /// its own bounds. To let the scale-up render fully, the window is temporarily enlarged (kept
    /// centered on its original position) for the animation's duration, then restored.
    /// </summary>
    private void PlayAttentionAnimation()
    {
        if (_attentionAnimationRunning || _displayMode == DisplayMode.Flex)
        {
            return;
        }

        _attentionAnimationRunning = true;

        var originalBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        const double peakScale = 1.15;
        var expandedWidth = (int)Math.Ceiling(originalBounds.Width * peakScale);
        var expandedHeight = (int)Math.Ceiling(originalBounds.Height * peakScale);
        var expandedX = originalBounds.X - (expandedWidth - originalBounds.Width) / 2;
        var expandedY = originalBounds.Y - (expandedHeight - originalBounds.Height) / 2;
        AppWindow.MoveAndResize(new(expandedX, expandedY, expandedWidth, expandedHeight));

        const double restOpacity = 1.0;

        var scaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = peakScale,
            Duration = TimeSpan.FromMilliseconds(140),
            AutoReverse = true,
            EasingFunction = new QuadraticEase()
        };
        var opacityAnimation = new DoubleAnimation
        {
            From = RootGrid.Opacity,
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(140),
            AutoReverse = true
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(scaleAnimation, AttentionScaleTransform);
        Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
        var scaleYAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = peakScale,
            Duration = TimeSpan.FromMilliseconds(140),
            AutoReverse = true,
            EasingFunction = new QuadraticEase()
        };
        Storyboard.SetTarget(scaleYAnimation, AttentionScaleTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");
        Storyboard.SetTarget(opacityAnimation, RootGrid);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

        storyboard.Children.Add(scaleAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Children.Add(opacityAnimation);
        storyboard.Completed += (_, _) =>
        {
            AppWindow.MoveAndResize(originalBounds);
            RootGrid.Opacity = restOpacity;
            _attentionAnimationRunning = false;
        };
        storyboard.Begin();
    }

    /// <summary>
    /// Re-applies <c>HWND_TOPMOST</c> when something (usually explorer.exe re-asserting the
    /// taskbar's own z-order on click) may have pushed this window behind it.
    /// </summary>
    private void ReassertTopmost()
    {
        if (_presenter.IsAlwaysOnTop)
        {
            TopmostHelper.ForceTopmost(Hwnd);
        }
    }

    /// <summary>
    /// Re-measures the current view's content and resizes the <c>AppWindow</c> to fit it (see
    /// <see cref="AutoSizingWindow.ResizeToContent"/>), replacing WPF's
    /// <c>SizeToContent=WidthAndHeight</c>. Called on load, language switch, and display-mode
    /// switch - the moments content naturally changes size - plus whenever the view model's
    /// formatted earnings text changes width (see <see cref="OnViewModelPropertyChanged"/>).
    /// </summary>
    private void ResizeToContent()
    {
        if (_displayMode == DisplayMode.Flex || _attentionAnimationRunning)
        {
            return;
        }

        ResizeToContent(RootGrid);
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        ApplyOpacity(1.0);

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e) =>
        ApplyOpacity(ViewModel.Opacity);

    /// <summary>
    /// Switches the visible content for <paramref name="mode"/> and resizes/repositions the
    /// window accordingly. <see cref="DisplayMode.Flex"/> fills the current monitor and disables
    /// content-based auto-sizing, matching the WPF build's <c>ApplyFlexBounds</c>.
    /// </summary>
    private void SetDisplayModeChrome(DisplayMode mode)
    {
        var wasFlex = _displayMode == DisplayMode.Flex;
        _displayMode = mode;

        NormalContent.Visibility = mode == DisplayMode.Normal ? Visibility.Visible : Visibility.Collapsed;
        MiniContent.Visibility = mode == DisplayMode.Mini ? Visibility.Visible : Visibility.Collapsed;
        FlexContent.Visibility = mode == DisplayMode.Flex ? Visibility.Visible : Visibility.Collapsed;

        if (mode == DisplayMode.None)
        {
            AppWindow.Hide();
            return;
        }

        if (!AppWindow.IsVisible)
        {
            AppWindow.Show();
        }

        if (mode == DisplayMode.Flex)
        {
            AppWindow.MoveAndResize(ScreenHelper.GetCurrentMonitorBounds(Hwnd));
        }
        else if (wasFlex && _pendingCenterScreen is { } screen)
        {
            _pendingCenterScreen = null;
            ResizeToContent();
            AppWindow.Move(new(
                screen.X + (screen.Width - AppWindow.Size.Width) / 2,
                screen.Y + (screen.Height - AppWindow.Size.Height) / 2));
        }
        else
        {
            ResizeToContent();
        }
    }

    /// <summary>
    /// Registers Escape-to-exit-Flex as a <see cref="KeyboardAccelerator"/>. A Win32 WndProc
    /// subclass (as used for the global hotkey) does not work here for the same reason drag via
    /// <c>WM_NCHITTEST</c> did not: keyboard focus lives in the child HWND hosting the XAML
    /// content, so the top-level window's WndProc never observes <c>WM_KEYDOWN</c>.
    /// </summary>
    private void SetupEscapeAccelerator()
    {
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            if (_displayMode == DisplayMode.Flex)
            {
                _pendingCenterScreen = ScreenHelper.GetCurrentMonitorBounds(Hwnd);
                ViewModel.SetNormalModeCommand.Execute(null);
                args.Handled = true;
            }
        };
        RootGrid.KeyboardAccelerators.Add(escape);
    }
}