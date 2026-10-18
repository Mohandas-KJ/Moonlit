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
