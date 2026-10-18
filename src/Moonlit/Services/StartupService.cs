using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Moonlit.Services;

/// <summary>
/// Result of a startup preference modification or verification.
/// </summary>
public sealed record StartupOperationResult(
    bool Success,
    bool IsEnabled,
    string Message
);

/// <summary>
/// Service managing the "Launch at Windows Sign-in / Boot" preference via per-user registry Run key.
/// </summary>
public sealed class StartupService
{
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Moonlit";

    /// <summary>
    /// Checks whether the application is currently registered to launch at user sign-in.
    /// </summary>
    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, writable: false);
            if (key == null)
            {
                return false;
            }

            var value = key.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var currentExecutable = GetExecutablePath();
            return value.Contains(currentExecutable, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Applies the user's startup preference and immediately verifies the result.
    /// Passes '--boot' flag so Moonlit immediately presents the cinematic lock screen.
    /// </summary>
    public StartupOperationResult SetStartupEnabled(bool enable)
    {
        try
        {
            var executablePath = GetExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return new StartupOperationResult(
                    Success: false,
                    IsEnabled: false,
                    Message: "Unable to determine current application executable path."
                );
            }

            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, writable: true))
            {
                if (key == null)
                {
                    return new StartupOperationResult(
                        Success: false,
                        IsEnabled: false,
                        Message: "Unable to access the Windows current user Run registry key."
                    );
                }

                if (enable)
                {
                    // Pass --boot so Moonlit starts directly as lock screen
                    key.SetValue(ValueName, $"\"{executablePath}\" --boot");
                }
                else
                {
                    if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, throwOnMissingValue: false);
                    }
                }
            }

            // Verification step: re-read the registry
            var isActuallyEnabled = IsStartupEnabled();
            if (isActuallyEnabled == enable)
            {
                return new StartupOperationResult(
                    Success: true,
                    IsEnabled: isActuallyEnabled,
                    Message: enable
                        ? "Launch at Windows sign-in has been enabled successfully."
                        : "Launch at Windows sign-in has been disabled."
                );
            }
            else
            {
                return new StartupOperationResult(
                    Success: false,
                    IsEnabled: isActuallyEnabled,
                    Message: "Verification failed: Registry startup state does not match the requested state."
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new StartupOperationResult(
                Success: false,
                IsEnabled: IsStartupEnabled(),
                Message: "Access denied when updating startup registry settings."
            );
        }
        catch (Exception ex)
        {
            return new StartupOperationResult(
                Success: false,
                IsEnabled: IsStartupEnabled(),
                Message: $"Failed to configure startup: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Creates convenient "Lock PC with Moonlit" shortcuts on Desktop and in Start Menu Programs.
    /// </summary>
    public static bool CreateLockShortcuts()
    {
        try
        {
            var exePath = GetExecutablePath();
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var programsPath = Path.Combine(startMenuPath, "Programs");

            var desktopShortcut = Path.Combine(desktopPath, "Lock PC with Moonlit.lnk").Replace("'", "''");
            var startMenuShortcut = Path.Combine(programsPath, "Lock PC with Moonlit.lnk").Replace("'", "''");
            var escapedExe = exePath.Replace("'", "''");

            var script = $@"
$w = New-Object -COM WScript.Shell
$s1 = $w.CreateShortcut('{desktopShortcut}')
$s1.TargetPath = '{escapedExe}'
$s1.Arguments = '--lock'
$s1.IconLocation = '{escapedExe},0'
$s1.Description = 'Lock PC with Moonlit Cinematic Lock Screen'
$s1.Save()

$s2 = $w.CreateShortcut('{startMenuShortcut}')
$s2.TargetPath = '{escapedExe}'
$s2.Arguments = '--lock'
$s2.IconLocation = '{escapedExe},0'
$s2.Description = 'Lock PC with Moonlit Cinematic Lock Screen'
$s2.Save()
";

            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return processPath;
        }

        return AppContext.BaseDirectory.TrimEnd('\\') + "\\Moonlit.exe";
    }
}
