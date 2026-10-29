namespace PayBeat.App.Platform;

/// <summary>
/// Creates the platform-specific service implementations, falling back to no-op versions on
/// platforms where a feature is not available.
/// </summary>
public static class PlatformServices
{
    /// <summary>
    /// Creates the global hotkey service for the current platform.
    /// </summary>
    /// <returns>A working service on Windows, otherwise a no-op one.</returns>
    public static IHotkeyService CreateHotkeyService() =>
        OperatingSystem.IsWindows() ? new WindowsHotkeyService() : new NullHotkeyService();

    /// <summary>
    /// Creates the "run at startup" service for the current platform.
    /// </summary>
    /// <returns>A working service on Windows, otherwise a no-op one.</returns>
    public static IStartupService CreateStartupService() =>
        OperatingSystem.IsWindows() ? new WindowsStartupService() : new NullStartupService();

    /// <summary>
    /// Creates the topmost z-order enforcer for the current platform.
    /// </summary>
    /// <returns>A working enforcer on Windows, otherwise a no-op one.</returns>
    public static ITopmostEnforcer CreateTopmostEnforcer() =>
        OperatingSystem.IsWindows() ? new WindowsTopmostEnforcer() : new NullTopmostEnforcer();
}
