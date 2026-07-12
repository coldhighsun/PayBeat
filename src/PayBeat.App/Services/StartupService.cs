using Microsoft.Win32;

namespace PayBeat.App.Services;

/// <summary>
/// Manages the Windows auto-startup registry entry for PayBeat under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PayBeat";

    /// <summary>
    /// Returns <see langword="true"/> when the PayBeat startup entry exists in the registry
    /// and points at the currently running executable's path.
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        if (key?.GetValue(ValueName) is not string value || string.IsNullOrEmpty(value))
        {
            return false;
        }

        var exe = GetCurrentExecutablePath();
        return !string.IsNullOrEmpty(exe) && string.Equals(value.Trim('"'), exe, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds or removes the startup registry entry.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to register; <see langword="false"/> to remove.</param>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var exe = GetCurrentExecutablePath();
            if (!string.IsNullOrEmpty(exe))
            {
                key.SetValue(ValueName, $"\"{exe}\"");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string? GetCurrentExecutablePath() =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
}