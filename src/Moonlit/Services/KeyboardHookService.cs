using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Moonlit.Services;

/// <summary>
/// Low-level global keyboard hook service monitoring for Win + L and global lock shortcuts.
/// </summary>
public sealed class KeyboardHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_L = 0x4C;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt key
    private const int VK_SHIFT = 0x10;
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SPACE = 0x20;
    private const int VK_F4 = 0x73;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _isDisposed;

    /// <summary>
    /// Event fired whenever the user triggers Win + L, Ctrl + Alt + L, or Win + Alt + L.
    /// </summary>
    public event EventHandler? LockRequested;

    public bool IsHookActive => _hookId != IntPtr.Zero;

    /// <summary>
    /// When true, Moonlit's lock experience window is active in full-screen kiosk overlay mode,
    /// so system breakout combinations (Alt+Tab, Alt+F4, Windows key, Ctrl+Esc) are suppressed.
    /// </summary>
    public bool IsKioskLockActive { get; set; }

    /// <summary>
    /// Indicates whether the most recently intercepted shortcut was specifically Win + L.
    /// </summary>
    public bool LastShortcutWasWinL { get; private set; }

    public KeyboardHookService()
    {
        // Maintain delegate reference to prevent garbage collection
        _proc = HookCallback;
    }

    public void StartHook()
    {
        if (_hookId != IntPtr.Zero) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        if (curModule?.ModuleName != null)
        {
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    public void StopHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var winDown = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
            var ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            var altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            var shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

            if (IsKioskLockActive)
            {
                // Suppress keys that attempt to switch away or close the lock screen without PIN
                if (vkCode == VK_TAB && altDown) return (IntPtr)1; // Alt + Tab
                if (vkCode == VK_ESCAPE && (altDown || ctrlDown)) return (IntPtr)1; // Alt + Esc or Ctrl + Esc
                if (vkCode == VK_F4 && altDown) return (IntPtr)1; // Alt + F4
                if (vkCode == VK_SPACE && altDown) return (IntPtr)1; // Alt + Space
                if (vkCode == VK_LWIN || vkCode == VK_RWIN) return (IntPtr)1; // Win key
            }

            if (vkCode == VK_L)
            {
                // Win + L, Ctrl + Alt + L, Win + Alt + L, or Ctrl + Shift + L
                if (winDown || (ctrlDown && altDown) || (ctrlDown && shiftDown))
                {
                    LastShortcutWasWinL = winDown && !ctrlDown;
                    LockRequested?.Invoke(this, EventArgs.Empty);
                    return (IntPtr)1; // Swallow key stroke
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopHook();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
