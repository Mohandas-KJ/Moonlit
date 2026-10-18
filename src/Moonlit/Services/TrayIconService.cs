using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace Moonlit.Services;

/// <summary>
/// Provides native Windows System Tray (Taskbar Notification Area) icon integration using Win32 Shell_NotifyIcon.
/// Supports left-click to restore/display MainWindow, right-click WPF context menu, and immediate clean process termination.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1024;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_INFO = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    private readonly Action _onOpenDashboard;
    private readonly Action _onLockNow;
    private readonly Action _onExit;

    private HwndSource? _hwndSource;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded;
    private bool _isDisposed;
    private ContextMenu? _contextMenu;

    public bool IsAdded => _isAdded;

    public TrayIconService(Action onOpenDashboard, Action onLockNow, Action onExit)
    {
        _onOpenDashboard = onOpenDashboard;
        _onLockNow = onLockNow;
        _onExit = onExit;
    }

    public void Initialize()
    {
        if (_isAdded || _isDisposed) return;

        // 1. Create a lightweight invisible HwndSource window to receive tray messages
        var parameters = new HwndSourceParameters("MoonlitTrayListener")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0x00000080, // WS_EX_TOOLWINDOW
            Width = 0,
            Height = 0
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        // 2. Load the application icon
        _hIcon = LoadIconInternal();

        // 3. Register icon with Windows Shell
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwndSource.Handle,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Moonlit — Cinematic Lock Screen (Active)"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            var mouseMsg = lParam.ToInt32();

            switch (mouseMsg)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    Application.Current.Dispatcher.Invoke(_onOpenDashboard);
                    handled = true;
                    break;

                case WM_RBUTTONUP:
                    Application.Current.Dispatcher.Invoke(ShowContextMenu);
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        if (_hwndSource != null)
        {
            SetForegroundWindow(_hwndSource.Handle);
        }

        if (_contextMenu == null)
        {
            _contextMenu = BuildContextMenu();
        }

        _contextMenu.Placement = PlacementMode.MousePoint;
        _contextMenu.IsOpen = true;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x1B, 0x22)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x42)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        // Header (Disabled, Title)
        var titleItem = new MenuItem
        {
            Header = "Moonlit — Cinematic Lock Screen",
            IsEnabled = false,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0xB4, 0xFF))
        };
        menu.Items.Add(titleItem);

        menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x42)) });

        // Open Dashboard
        var openItem = new MenuItem
        {
            Header = "Open Settings Dashboard",
            Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7))
        };
        openItem.Click += (_, _) => _onOpenDashboard();
        menu.Items.Add(openItem);

        // Lock PC Now
        var lockItem = new MenuItem
        {
            Header = "Lock PC with Moonlit (Win + L)",
            Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7))
        };
        lockItem.Click += (_, _) => _onLockNow();
        menu.Items.Add(lockItem);

        menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x42)) });

        // Exit Moonlit
        var exitItem = new MenuItem
        {
            Header = "Exit Moonlit",
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x73, 0x7B)),
            FontWeight = FontWeights.Medium
        };
        exitItem.Click += (_, _) => _onExit();
        menu.Items.Add(exitItem);

        return menu;
    }

    public void ShowBalloon(string title, string message)
    {
        if (!_isAdded || _hwndSource == null) return;

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwndSource.Handle,
            uID = 1001,
            uFlags = NIF_INFO,
            szInfoTitle = title.Length > 63 ? title[..63] : title,
            szInfo = message.Length > 255 ? message[..255] : message,
            dwInfoFlags = (int)NIIF_INFO
        };

        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private IntPtr LoadIconInternal()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                var h = ExtractIcon(IntPtr.Zero, exePath, 0);
                if (h != IntPtr.Zero) return h;
            }

            var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");
            if (File.Exists(assetPath))
            {
                var h = LoadImage(IntPtr.Zero, assetPath, 1, 16, 16, 0x0010);
                if (h != IntPtr.Zero) return h;
            }
        }
        catch
        {
        }

        // Standard application icon fallback
        return LoadIcon(IntPtr.Zero, (IntPtr)32512);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_isAdded && _hwndSource != null)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _isAdded = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }
}
