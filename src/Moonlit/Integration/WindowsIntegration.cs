using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace Moonlit.Integration;

/// <summary>
/// Encapsulates native Windows platform APIs, workstation locking, keystroke simulation, and policies.
/// </summary>
public static class WindowsIntegration
{
    private const string PoliciesSystemKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string DisableLockValue = "DisableLockWorkstation";

    private const byte VK_SPACE = 0x20;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>
    /// Calls the official Windows user32.dll LockWorkStation API to lock the workstation,
    /// triggering the genuine Windows authentication and sign-in screen (PIN, Windows Hello, Password).
    /// </summary>
    public static bool TriggerWindowsLock()
    {
        try
        {
            // If workstation locking was disabled in registry for Win+L interception,
            // temporarily enable it so Windows will accept the lock command
            var wasDisabled = IsLockWorkstationDisabled();
            if (wasDisabled)
            {
                SetLockWorkstationDisabled(false);
            }

            var locked = LockWorkStation();

            if (wasDisabled)
            {
                // Re-disable locking asynchronously after the lock takes effect
                Task.Delay(500).ContinueWith(_ => SetLockWorkstationDisabled(true));
            }

            return locked;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a synthetic keystroke (Space) to dismiss the Windows lock screen clock curtain
    /// and jump directly into the Password / PIN entry prompt.
    /// </summary>
    public static void SendDismissKeystroke()
    {
        try
        {
            // Small pause to allow desktop switch to register the input
            keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>
    /// Checks whether the Windows lock workstation policy (DisableLockWorkstation) is enabled.
    /// </summary>
    public static bool IsLockWorkstationDisabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PoliciesSystemKey, writable: false);
            if (key?.GetValue(DisableLockValue) is int val)
            {
                return val == 1;
            }
        }
        catch
        {
            // Fall through
        }
        return false;
    }

    /// <summary>
    /// Sets or removes DisableLockWorkstation in HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System.
    /// Returns true if successfully updated, or false if elevation is needed.
    /// </summary>
    public static bool SetLockWorkstationDisabled(bool disable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PoliciesSystemKey, writable: true);
            if (key != null)
            {
                if (disable)
                {
                    key.SetValue(DisableLockValue, 1, RegistryValueKind.DWord);
                }
                else
                {
                    key.DeleteValue(DisableLockValue, throwOnMissingValue: false);
                }
                return true;
            }
        }
        catch
        {
            // Standard user access to Policies may be restricted on some Windows builds
        }
        return false;
    }

    /// <summary>
    /// Invokes an elevated registry modification via reg.exe to toggle DisableLockWorkstation.
    /// </summary>
    public static bool SetLockWorkstationDisabledElevated(bool disable)
    {
        try
        {
            var command = disable
                ? $"add \"HKCU\\{PoliciesSystemKey}\" /v {DisableLockValue} /t REG_DWORD /d 1 /f"
                : $"delete \"HKCU\\{PoliciesSystemKey}\" /v {DisableLockValue} /f";

            var startInfo = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = command,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets primary screen bounds.
    /// </summary>
    public static Rect GetPrimaryScreenBounds()
    {
        return new Rect(
            0,
            0,
            SystemParameters.PrimaryScreenWidth,
            SystemParameters.PrimaryScreenHeight
        );
    }

    /// <summary>
    /// Gets virtual screen bounds covering all attached monitors.
    /// </summary>
    public static Rect GetVirtualScreenBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight
        );
    }
}
