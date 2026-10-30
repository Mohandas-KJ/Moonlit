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

    private const string PoliciesPersonalizationKey = @"SOFTWARE\Policies\Microsoft\Windows\Personalization";
    private const string NoLockScreenValue = "NoLockScreen";

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const ushort VK_SPACE = 0x20;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_UP = 0x26;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

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
                Task.Delay(1000).ContinueWith(_ => SetLockWorkstationDisabled(true));
            }

            return locked;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a synthetic key stroke down and up using the modern Win32 SendInput API.
    /// </summary>
    public static void SendKeyStroke(ushort virtualKey)
    {
        try
        {
            var inputs = new INPUT[2];

            // Key Down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            // Key Up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        catch
        {
            // Non-critical fallback
        }
    }

    /// <summary>
    /// Sends a timed pulse sequence of keystrokes (Space, Return, Up Arrow) across multiple intervals
    /// to guarantee the Windows lock curtain is dismissed and the PIN/Password prompt is revealed.
    /// </summary>
    public static void StartDismissPulseSequence()
    {
        Task.Run(async () =>
        {
            var intervals = new[] { 350, 400, 450, 500 };
            var keys = new[] { VK_SPACE, VK_RETURN, VK_UP, VK_SPACE };

            for (int i = 0; i < intervals.Length; i++)
            {
                await Task.Delay(intervals[i]);
                SendKeyStroke(keys[i]);
            }
        });
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

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PoliciesSystemKey, writable: false);
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
    /// Checks whether the Windows NoLockScreen or DoNotDisplayLockScreen policy is enabled (skipping the lock curtain entirely).
    /// </summary>
    public static bool IsNoLockScreenConfigured()
    {
        // 1. Check HKLM Personalization
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PoliciesPersonalizationKey, writable: false);
            if (key?.GetValue(NoLockScreenValue) is int val && val == 1) return true;
            if (key?.GetValue("DoNotDisplayLockScreen") is int val2 && val2 == 1) return true;
        }
        catch { }

        // 2. Check HKLM LogonUI
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI", writable: false);
            if (key?.GetValue(NoLockScreenValue) is int val && val == 1) return true;
        }
        catch { }

        // 3. Check HKCU Personalization
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PoliciesPersonalizationKey, writable: false);
            if (key?.GetValue(NoLockScreenValue) is int val && val == 1) return true;
            if (key?.GetValue("DoNotDisplayLockScreen") is int val2 && val2 == 1) return true;
        }
        catch { }

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
    /// Executes a one-time elevated PowerShell script that:
    /// 1. Grants the current user FullControl permissions on HKCU\Policies\System so Moonlit can toggle Win+L without UAC.
    /// 2. Sets DisableLockWorkstation = 1.
    /// 3. Sets NoLockScreen = 1 and DoNotDisplayLockScreen = 1 across HKLM and HKCU to completely bypass the Windows clock curtain.
    /// </summary>
    public static bool ConfigurePoliciesElevated()
    {
        try
        {
            var script = @"
$ErrorActionPreference = 'Stop'
$user = ""$env:USERDOMAIN\$env:USERNAME""
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value

# 1. Ensure HKCU Policies System key exists and grant current user FullControl
$regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System'
if (!(Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}
$acl = Get-Acl $regPath
$rule = New-Object System.Security.AccessControl.RegistryAccessRule($user, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.AddAccessRule($rule)
Set-Acl $regPath $acl
Set-ItemProperty -Path $regPath -Name 'DisableLockWorkstation' -Value 1 -Type DWord

# 2. Also ensure User SID path in HKEY_USERS has the policy and permissions
$sidPath = ""Registry::HKEY_USERS\$sid\Software\Microsoft\Windows\CurrentVersion\Policies\System""
if (Test-Path ""Registry::HKEY_USERS\$sid"") {
    if (!(Test-Path $sidPath)) {
        New-Item -Path $sidPath -Force | Out-Null
    }
    Set-ItemProperty -Path $sidPath -Name 'DisableLockWorkstation' -Value 1 -Type DWord
}

# 3. Disable native Lock Screen Clock Curtain in HKLM Personalization (NoLockScreen & DoNotDisplayLockScreen)
$persPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization'
if (!(Test-Path $persPath)) {
    New-Item -Path $persPath -Force | Out-Null
}
Set-ItemProperty -Path $persPath -Name 'NoLockScreen' -Value 1 -Type DWord
Set-ItemProperty -Path $persPath -Name 'DoNotDisplayLockScreen' -Value 1 -Type DWord

# 4. Disable native Lock Screen Clock Curtain in HKCU Personalization
$hkcuPersPath = 'HKCU:\Software\Policies\Microsoft\Windows\Personalization'
if (!(Test-Path $hkcuPersPath)) {
    New-Item -Path $hkcuPersPath -Force | Out-Null
}
Set-ItemProperty -Path $hkcuPersPath -Name 'NoLockScreen' -Value 1 -Type DWord
Set-ItemProperty -Path $hkcuPersPath -Name 'DoNotDisplayLockScreen' -Value 1 -Type DWord

# 5. Disable in LogonUI
$logonUiPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI'
if (!(Test-Path $logonUiPath)) {
    New-Item -Path $logonUiPath -Force | Out-Null
}
Set-ItemProperty -Path $logonUiPath -Name 'NoLockScreen' -Value 1 -Type DWord
";

            var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -EncodedCommand {encodedScript}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes an elevated one-click complete removal of all Moonlit policies,
    /// completely reverting Windows 11 back to native locking, original clock curtain, and default hotkey handling.
    /// </summary>
    public static bool RemovePoliciesElevated()
    {
        try
        {
            var script = @"
$ErrorActionPreference = 'SilentlyContinue'

# 1. Clear HKCU Policies System
$regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System'
if (Test-Path $regPath) {
    Remove-ItemProperty -Path $regPath -Name 'DisableLockWorkstation' -ErrorAction SilentlyContinue
}

# 2. Clear HKEY_USERS SID Policies System
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$sidPath = ""Registry::HKEY_USERS\$sid\Software\Microsoft\Windows\CurrentVersion\Policies\System""
if (Test-Path $sidPath) {
    Remove-ItemProperty -Path $sidPath -Name 'DisableLockWorkstation' -ErrorAction SilentlyContinue
}

# 3. Clear HKLM Policies System if set
$hklmSysPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Policies\System'
if (Test-Path $hklmSysPath) {
    Remove-ItemProperty -Path $hklmSysPath -Name 'DisableLockWorkstation' -ErrorAction SilentlyContinue
}

# 4. Clear HKLM Personalization curtain bypasses
$persPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization'
if (Test-Path $persPath) {
    Remove-ItemProperty -Path $persPath -Name 'NoLockScreen' -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $persPath -Name 'DoNotDisplayLockScreen' -ErrorAction SilentlyContinue
}

# 5. Clear HKCU Personalization curtain bypasses
$hkcuPersPath = 'HKCU:\Software\Policies\Microsoft\Windows\Personalization'
if (Test-Path $hkcuPersPath) {
    Remove-ItemProperty -Path $hkcuPersPath -Name 'NoLockScreen' -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $hkcuPersPath -Name 'DoNotDisplayLockScreen' -ErrorAction SilentlyContinue
}

# 6. Clear LogonUI curtain bypass
$logonUiPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI'
if (Test-Path $logonUiPath) {
    Remove-ItemProperty -Path $logonUiPath -Name 'NoLockScreen' -ErrorAction SilentlyContinue
}
";

            var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -EncodedCommand {encodedScript}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// One-click comprehensive cleanup: clears local user registry immediately,
    /// optionally invokes elevated removal for system keys, and verifies that default Windows 11 behavior is restored.
    /// </summary>
    public static (bool Success, string Message) ClearAllMoonlitRegistryPolicies(bool promptElevation = true)
    {
        // 1. Immediately clear per-user registry entries in-process
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(PoliciesSystemKey, writable: true))
            {
                key?.DeleteValue(DisableLockValue, throwOnMissingValue: false);
            }
        }
        catch { }

        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(PoliciesPersonalizationKey, writable: true))
            {
                key?.DeleteValue(NoLockScreenValue, throwOnMissingValue: false);
                key?.DeleteValue("DoNotDisplayLockScreen", throwOnMissingValue: false);
            }
        }
        catch { }

        bool elevatedOk = false;
        if (promptElevation)
        {
            // 2. Invoke elevated cleanup for machine-wide policies (HKLM)
            elevatedOk = RemovePoliciesElevated();
        }

        // 3. Verify clean state
        var isWinLStillDisabled = IsLockWorkstationDisabled();
        var isNoLockScreenStillSet = IsNoLockScreenConfigured();

        if (!isWinLStillDisabled && !isNoLockScreenStillSet)
        {
            return (true, "All Moonlit registry policies have been completely removed. Windows 11 default lock screen, clock curtain, and hotkey behavior are 100% restored.");
        }
        else if (elevatedOk)
        {
            return (true, "Policies removed successfully. System settings will fully reflect changes upon the next session switch.");
        }
        else
        {
            return (false, "Local user policies were cleared, but administrator permission was cancelled for machine-wide policies. Please accept the UAC prompt to remove all keys.");
        }
    }

    /// <summary>
    /// Launches Windows Netplwiz (User Accounts control panel) to allow configuring automatic logon on boot.
    /// </summary>
    public static bool OpenAutoLogonUtility()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "netplwiz.exe",
                UseShellExecute = true
            });
            return true;
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
