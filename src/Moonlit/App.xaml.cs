using System.Threading;
using System.Windows;
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

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _lockEventHandle;
    private CancellationTokenSource? _cts;

    private static SettingsService? _settingsService;
    private static LockSessionManager? _lockSessionManager;

    public static SettingsService SettingsService => _settingsService ??= new SettingsService();
    public static LockSessionManager LockSessionManager => _lockSessionManager ??= new LockSessionManager(SettingsService);

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

        // Prevent duplicate instances of Moonlit
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
                    SettingsService.LogMessage($"Failed to signal existing Moonlit instance: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show(
                    "Another instance of Moonlit is already active and running in the background.",
                    "Moonlit",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            Shutdown();
            return;
        }

        // Keep app alive in background for lock session / hotkey monitoring
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialize IPC named event listener for external lock requests (shortcuts, scripts)
        _lockEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, LockEventName);
        _cts = new CancellationTokenSource();
        var cancelToken = _cts.Token;

        Task.Run(() =>
        {
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    if (_lockEventHandle.WaitOne(1000))
                    {
                        if (cancelToken.IsCancellationRequested) break;
                        Dispatcher.Invoke(() =>
                        {
                            var currentSettings = SettingsService.LoadSettings();
                            LockSessionManager.LaunchLockExperience(currentSettings, lockOnDismiss: true);
                        });
                    }
                }
                catch
                {
                    // Handle disposal during exit
                }
            }
        }, cancelToken);

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
            // Crucially, lockOnDismiss MUST be true so dismissing it transitions to Windows PIN/Password authentication,
            // making it function and feel like an authentic, secure lock screen!
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
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
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
        _cts?.Cancel();
        _cts?.Dispose();
        _lockEventHandle?.Dispose();
        _lockSessionManager?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
