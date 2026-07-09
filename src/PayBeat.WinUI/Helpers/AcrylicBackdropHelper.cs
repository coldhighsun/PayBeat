using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace PayBeat.WinUI.Helpers;

/// <summary>
/// Drives a <see cref="DesktopAcrylicController"/> attached to a <see cref="Window"/>, replacing
/// the WPF build's approach of fading the whole window via <c>Window.Opacity</c>/content opacity.
/// The controller's <see cref="DesktopAcrylicController.TintOpacity"/> is instead driven from the
/// settings-configured opacity value, so the window chrome/backdrop becomes see-through while
/// text and other content stay fully legible. Falls back to a plain transparent backdrop (no
/// blur) on systems where acrylic isn't supported (see <see cref="DesktopAcrylicController.IsSupported"/>).
/// </summary>
public sealed class AcrylicBackdropHelper : IDisposable
{
    private readonly DesktopAcrylicController? _controller;
    private readonly SystemBackdropConfiguration _configuration;
    private readonly Window _window;

    public AcrylicBackdropHelper(Window window, double initialOpacity)
    {
        _window = window;
        _configuration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark
        };

        _window.Activated += OnWindowActivated;
        _window.Closed += OnWindowClosed;

        if (!DesktopAcrylicController.IsSupported())
        {
            return;
        }

        _controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin
        };
        SetOpacity(initialOpacity);
        _controller.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
        _controller.SetSystemBackdropConfiguration(_configuration);
    }

    /// <summary>
    /// Maps the settings opacity (0.1-1.0, where 1.0 means fully opaque widget) directly onto the
    /// controller's tint/luminosity opacity, so the backdrop matches the same fully-opaque/fully-
    /// see-through direction as each view's <c>BackgroundBorder.Opacity</c> (see
    /// <see cref="Views.NormalView.SetBackgroundOpacity"/>) - otherwise the backdrop stays tinted
    /// at low settings opacity, right when the border is faded out and the backdrop should show
    /// through instead.
    /// </summary>
    public void SetOpacity(double opacity)
    {
        if (_controller is null)
        {
            return;
        }

        var tint = Math.Clamp(opacity, 0.0, 1.0);
        _controller.TintOpacity = (float)tint;
        _controller.LuminosityOpacity = (float)tint;
    }

    public void Dispose()
    {
        _window.Activated -= OnWindowActivated;
        _window.Closed -= OnWindowClosed;
        _controller?.Dispose();
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args) =>
        _configuration.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;

    private void OnWindowClosed(object sender, WindowEventArgs args) => Dispose();
}
