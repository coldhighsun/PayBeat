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
    /// and points at the currently running executable's path, <see langword="false"/> when it
    /// doesn't, or <see langword="null"/> when the registry couldn't be read (so callers don't
    /// mistake "unknown" for "not registered").
    /// </summary>
    public static bool? IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            if (key?.GetValue(ValueName) is not string value || string.IsNullOrEmpty(value))
            {
                return false;
            }

            var exe = GetCurrentExecutablePath();
            return !string.IsNullOrEmpty(exe) && string.Equals(value.Trim('"'), exe, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Adds or removes the startup registry entry.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to register; <see langword="false"/> to remove.</param>
    /// <returns><see langword="true"/> if the registry was successfully updated; <see langword="false"/>
    /// if a permission or access error prevented it.</returns>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
            {
                return false;
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

            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Best-effort; a locked-down machine shouldn't crash the app over a startup toggle.
            return false;
        }
    }

    private static string? GetCurrentExecutablePath() =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
}