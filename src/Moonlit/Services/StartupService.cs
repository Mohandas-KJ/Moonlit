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
/// Service managing the "Instant Launch at Windows Sign-in / Boot" preference via Windows Task Scheduler.
/// Runs Moonlit with zero delay upon user logon in the interactive user session.
/// Automatically migrates legacy per-user registry entries to Task Scheduler.
/// </summary>
public sealed class StartupService
{
    private const string TaskName = "Moonlit";
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Moonlit";

    /// <summary>
    /// Checks whether Moonlit is registered for instant startup via Windows Task Scheduler
    /// or legacy registry Run key.
    /// </summary>
    public bool IsStartupEnabled()
    {
        if (IsTaskSchedulerEnabled())
        {
            return true;
        }

        return IsLegacyRegistryStartupPresent();
    }

    /// <summary>
    /// Checks whether the Windows Task Scheduler startup task exists and is active.
    /// </summary>
    public static bool IsTaskSchedulerEnabled()
    {
        try
        {
            var scheduleType = Type.GetTypeFromProgID("Schedule.Service");
            if (scheduleType == null) return false;

            dynamic scheduler = Activator.CreateInstance(scheduleType)!;
            scheduler.Connect();

            dynamic rootFolder = scheduler.GetFolder(@"\");
            dynamic? task = rootFolder.GetTask(TaskName);
            if (task == null) return false;
            return (bool)task.Enabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether a legacy startup entry exists in HKCU Run registry.
    /// </summary>
    public static bool IsLegacyRegistryStartupPresent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, writable: false);
            if (key == null) return false;

            var val = key.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(val)) return false;

            var exe = GetExecutablePath();
            return val.Contains(exe, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// If an existing Moonlit Registry Run entry is detected, safely migrates the configuration
    /// to Windows Task Scheduler and removes the old entry only after successful registration.
    /// </summary>
    public void MigrateFromRegistryIfNeeded()
    {
        try
        {
            if (IsLegacyRegistryStartupPresent() && !IsTaskSchedulerEnabled())
            {
                var exe = GetExecutablePath();
                var registered = RegisterTaskSchedulerTask(exe);
                if (registered)
                {
                    RemoveLegacyRegistryEntry();
                    SettingsService.Log("Successfully migrated startup registration from Registry Run to Windows Task Scheduler.");
                }
            }
        }
        catch (Exception ex)
        {
            SettingsService.Log($"Startup migration notice: {ex.Message}");
        }
    }

    /// <summary>
    /// Enables or disables instant startup via Windows Task Scheduler.
    /// Passes '--boot' flag so Moonlit immediately presents the full-screen cinematic lock experience.
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

            if (enable)
            {
                var registered = RegisterTaskSchedulerTask(executablePath);
                if (registered)
                {
                    RemoveLegacyRegistryEntry();
                    return new StartupOperationResult(
                        Success: true,
                        IsEnabled: true,
                        Message: "Instant startup enabled via Windows Task Scheduler (At Log On, zero delay)."
                    );
                }
                else
                {
                    return new StartupOperationResult(
                        Success: false,
                        IsEnabled: IsStartupEnabled(),
                        Message: "Failed to register Windows Task Scheduler task."
                    );
                }
            }
            else
            {
                DeleteTaskSchedulerTask();
                RemoveLegacyRegistryEntry();

                return new StartupOperationResult(
                    Success: true,
                    IsEnabled: false,
                    Message: "Instant startup has been disabled."
                );
            }
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

    private static bool RegisterTaskSchedulerTask(string executablePath)
    {
        try
        {
            var scheduleType = Type.GetTypeFromProgID("Schedule.Service");
            if (scheduleType == null) return false;

            dynamic scheduler = Activator.CreateInstance(scheduleType)!;
            scheduler.Connect();

            dynamic rootFolder = scheduler.GetFolder(@"\");
            dynamic taskDef = scheduler.NewTask(0);

            // 1. Current user identity for interactive desktop session
            var currentIdentity = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            taskDef.Principal.UserId = currentIdentity;
            taskDef.Principal.LogonType = 3; // TASK_LOGON_INTERACTIVE_TOKEN

            // 2. Trigger: At Log On without delay (TASK_TRIGGER_LOGON = 9)
            dynamic trigger = taskDef.Triggers.Create(9);
            trigger.UserId = currentIdentity;

            // 3. Action: Launch Moonlit with --boot (TASK_ACTION_EXEC = 0)
            dynamic action = taskDef.Actions.Create(0);
            action.Path = executablePath;
            action.Arguments = "--boot";

            // 4. Performance & Reliability Settings
            taskDef.Settings.DisallowStartIfOnBatteries = false;
            taskDef.Settings.StopIfGoingOnBatteries = false;
            taskDef.Settings.ExecutionTimeLimit = "PT0S"; // Infinite limit, does not kill app
            taskDef.Settings.Priority = 4; // Interactive / normal priority
            taskDef.Settings.AllowDemandStart = true;
            taskDef.Settings.StartWhenAvailable = true;

            // 5. Register with TASK_CREATE_OR_UPDATE (6) and TASK_LOGON_INTERACTIVE_TOKEN (3)
            rootFolder.RegisterTaskDefinition(
                TaskName,
                taskDef,
                6, // TASK_CREATE_OR_UPDATE
                null,
                null,
                3  // TASK_LOGON_INTERACTIVE_TOKEN
            );

            return true;
        }
        catch (Exception ex)
        {
            SettingsService.Log($"RegisterTaskSchedulerTask failed: {ex.Message}");
            return false;
        }
    }

    private static void DeleteTaskSchedulerTask()
    {
        try
        {
            var scheduleType = Type.GetTypeFromProgID("Schedule.Service");
            if (scheduleType == null) return;

            dynamic scheduler = Activator.CreateInstance(scheduleType)!;
            scheduler.Connect();

            dynamic rootFolder = scheduler.GetFolder(@"\");
            rootFolder.DeleteTask(TaskName, 0);
        }
        catch
        {
            // Task might not exist, ignore
        }
    }

    private static void RemoveLegacyRegistryEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, writable: true);
            if (key?.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-critical
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
