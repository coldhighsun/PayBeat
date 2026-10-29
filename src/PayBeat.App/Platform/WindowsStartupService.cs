using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PayBeat.App.Platform;

/// <summary>
/// Manages the Windows auto-startup registry entry for PayBeat under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupService : IStartupService
{
    /// <summary>
    /// Registry key that holds per-user startup entries.
    /// </summary>
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Name of the value that stores PayBeat's startup command.
    /// </summary>
    private const string ValueName = "PayBeat";

    /// <inheritdoc/>
    public bool IsSupported => true;

    /// <inheritdoc/>
    public bool? IsEnabled()
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

    /// <inheritdoc/>
    public bool SetEnabled(bool enabled)
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

    /// <summary>
    /// Returns the full path of the running executable.
    /// </summary>
    private static string? GetCurrentExecutablePath() =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
}
