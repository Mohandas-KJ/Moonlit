using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Moonlit.Services;

namespace Moonlit;

/// <summary>
/// Interaction logic for App.xaml — Lifecycle management, boot handling, and background lock session coordination.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\Moonlit_SingleInstance_Mutex";
    private const string LockEventName = @"Local\Moonlit_LockRequested_Event";
    private const string ShowWindowEventName = @"Local\Moonlit_ShowWindow_Event";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _lockEventHandle;
    private static EventWaitHandle? _showWindowEventHandle;
    private static CancellationTokenSource? _cts;

    private static SettingsService? _settingsService;
    private static LockSessionManager? _lockSessionManager;
    private static TrayIconService? _trayIconService;

    public static SettingsService SettingsService => _settingsService ??= new SettingsService();
    public static LockSessionManager LockSessionManager => _lockSessionManager ??= new LockSessionManager(SettingsService);
    public static TrayIconService? TrayIconService => _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception logging
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        SettingsService.LogMessage($"Moonlit application launched with args: {string.Join(" ", e.Args)}");

        // Parse command line arguments
        bool isLockMode = false;
        bool isBootMode = false;
        bool isSetupPoliciesMode = false;

        foreach (var arg in e.Args)
        {
            if (arg.Equals("--lock", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-l", StringComparison.OrdinalIgnoreCase))
            {
                isLockMode = true;
            }
            else if (arg.Equals("--boot", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-b", StringComparison.OrdinalIgnoreCase))
            {
                isBootMode = true;
            }
            else if (arg.Equals("--setup-policies", StringComparison.OrdinalIgnoreCase))
            {
                isSetupPoliciesMode = true;
            }
        }

        // Handle one-time elevated policy setup switch
        if (isSetupPoliciesMode)
        {
            var success = Moonlit.Integration.WindowsIntegration.ConfigurePoliciesElevated();
            MessageBox.Show(
                success
                    ? "Moonlit system integration policies have been successfully configured.\n\nWin+L direct interception and NoLockScreen curtain bypass are now active."
                    : "Failed to configure policies or administrator privileges were declined.",
                "Moonlit Setup",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning
            );
            Shutdown();
            return;
        }

        // Prevent duplicate instances of Moonlit and activate existing running instance
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            if (isLockMode)
            {
                // Signal existing instance to immediately launch lock screen
                try
                {
                    using var existingEvent = EventWaitHandle.OpenExisting(LockEventName);
                    existingEvent.Set();
                }
                catch (Exception ex)
                {
                    SettingsService.LogMessage($"Failed to signal existing Moonlit instance lock event: {ex.Message}");
                }
            }
            else
            {
                // If Moonlit is already running, immediately restore/display MainWindow to the user
                try
                {
                    using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                    showEvent.Set();
                }
                catch (Exception ex)
                {
                    SettingsService.LogMessage($"Failed to signal existing Moonlit instance show event: {ex.Message}");
                }
            }
            Shutdown();
            return;
        }

        // Keep app alive in background for lock session / hotkey monitoring
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialize IPC named event listeners for external lock & show requests
        _lockEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, LockEventName);
        _showWindowEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        _cts = new CancellationTokenSource();
        var cancelToken = _cts.Token;

        var waitHandles = new WaitHandle[] { _lockEventHandle, _showWindowEventHandle, cancelToken.WaitHandle };

        Task.Run(() =>
        {
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    var triggeredIndex = WaitHandle.WaitAny(waitHandles);
                    if (cancelToken.IsCancellationRequested) break;

                    if (triggeredIndex == 0) // Lock requested
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var currentSettings = SettingsService.LoadSettings();
                            LockSessionManager.LaunchLockExperience(currentSettings, lockOnDismiss: true);
                        });
                    }
                    else if (triggeredIndex == 1) // Show MainWindow requested
                    {
                        Dispatcher.Invoke(ShowMainWindow);
                    }
                }
                catch
                {
                    // Handle disposal during exit
                }
            }
        }, cancelToken);

        // Initialize System Tray Icon
        _trayIconService = new TrayIconService(
            onOpenDashboard: ShowMainWindow,
            onLockNow: () => LockSessionManager.LockNow(),
            onExit: ExitApplication
        );
        _trayIconService.Initialize();

        // Ensure LockSessionManager is instantiated to begin monitoring Win+L and session events
        var sessionManager = LockSessionManager;
        var settings = SettingsService.LoadSettings();

        if (isLockMode)
        {
            // Immediate lock request via command-line shortcut
            sessionManager.LaunchLockExperience(settings, lockOnDismiss: true);
        }
        else if (isBootMode)
        {
            // Issue 5: When starting on boot, if configured, present the cinematic lock screen.
            // Crucially, lockOnDismiss MUST be true so dismissing it transitions to Windows PIN/Password authentication
            if (settings.LockExperienceEnabled && settings.StartOnBootAsLockScreen && !string.IsNullOrWhiteSpace(settings.VideoPath))
            {
                SettingsService.LogMessage("Boot launch detected: Presenting initial cinematic lock screen experience (lockOnDismiss = true).");
                sessionManager.LaunchLockExperience(settings, lockOnDismiss: true);
            }
            else
            {
                SettingsService.LogMessage("Boot launch detected: Running silently in background monitoring lock events.");
            }
        }
        else
        {
            // Standard launch: open settings dashboard
            ShowMainWindow();
        }
    }

    /// <summary>
    /// Displays or restores the MainWindow settings dashboard to the front of the screen.
    /// </summary>
    public static void ShowMainWindow()
    {
        Current.Dispatcher.Invoke(() =>
        {
            if (Current.MainWindow is MainWindow existingWindow && existingWindow.IsLoaded)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }
                existingWindow.Show();
                existingWindow.Activate();
                existingWindow.Focus();

                var helper = new WindowInteropHelper(existingWindow);
                if (helper.Handle != IntPtr.Zero)
                {
                    SetForegroundWindow(helper.Handle);
                }
            }
            else
            {
                var newWindow = new MainWindow();
                Current.MainWindow = newWindow;
                newWindow.Show();
                newWindow.Activate();
                newWindow.Focus();

                var helper = new WindowInteropHelper(newWindow);
                if (helper.Handle != IntPtr.Zero)
                {
                    SetForegroundWindow(helper.Handle);
                }
            }
        });
    }

    /// <summary>
    /// Completely and immediately stops Moonlit, removes the tray icon, terminates background threads, and exits.
    /// </summary>
    public static void ExitApplication()
    {
        SettingsService.LogMessage("Immediate exit requested by user.");

        try
        {
            _trayIconService?.Dispose();
            _trayIconService = null;
        }
        catch { }

        try
        {
            _lockSessionManager?.Dispose();
            _lockSessionManager = null;
        }
        catch { }

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch { }

        try
        {
            _lockEventHandle?.Dispose();
            _lockEventHandle = null;

            _showWindowEventHandle?.Dispose();
            _showWindowEventHandle = null;
        }
        catch { }

        try
        {
            Current?.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Current.Windows)
                {
                    try { window.Close(); } catch { }
                }
            });
        }
        catch { }

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
        catch { }

        Environment.Exit(0);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SettingsService.LogMessage($"Unhandled UI Exception: {e.Exception}");
        MessageBox.Show(
            $"An unexpected error occurred:\n{e.Exception.Message}",
            "Moonlit Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            SettingsService.LogMessage($"Unhandled Domain Exception: {ex}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsService.LogMessage($"Moonlit application exiting with code {e.ApplicationExitCode}.");
        ExitApplication();
        base.OnExit(e);
    }
}
