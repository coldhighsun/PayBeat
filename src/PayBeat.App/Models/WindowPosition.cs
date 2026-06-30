namespace PayBeat.App.Models;

/// <summary>
/// Stores a saved window position together with the device name of the screen it was last on,
/// used to restore the widget to the correct monitor across restarts.
/// </summary>
/// <param name="Left">Window left edge in WPF logical units.</param>
/// <param name="Top">Window top edge in WPF logical units.</param>
/// <param name="ScreenDeviceName">
/// Win32 device name of the monitor (e.g. <c>\\.\DISPLAY1</c>).
/// <see langword="null"/> when the position was saved without multi-monitor awareness.
/// </param>
public record WindowPosition(double Left, double Top, string? ScreenDeviceName = null);