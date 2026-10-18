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
    private Mutex? _singleInstanceMutex;

    private static SettingsService? _settingsService;
    private static LockSessionManager? _lockSessionManager;

    public static SettingsService SettingsService => _settingsService ??= new SettingsService();
    public static LockSessionManager LockSessionManager => _lockSessionManager ??= new LockSessionManager(SettingsService);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent duplicate instances of Moonlit
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            MessageBox.Show(
                "Another instance of Moonlit is already active.",
                "Moonlit",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            Shutdown();
            return;
        }

        // Global exception logging
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        SettingsService.LogMessage($"Moonlit application launched with args: {string.Join(" ", e.Args)}");

        // Keep app alive in background for lock session / hotkey monitoring
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Ensure LockSessionManager is instantiated to begin monitoring Win+L and session events
        var sessionManager = LockSessionManager;

        var isBootMode = false;
        foreach (var arg in e.Args)
        {
            if (arg.Equals("--boot", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-b", StringComparison.OrdinalIgnoreCase))
            {
                isBootMode = true;
                break;
            }
        }

        var settings = SettingsService.LoadSettings();

        // Issue 3: "If user selects start with Boot! It should play first on the lock screen and should give the feel that it's the lockscreen!"
        if (isBootMode && settings.LockExperienceEnabled && settings.StartOnBootAsLockScreen && !string.IsNullOrWhiteSpace(settings.VideoPath))
        {
            SettingsService.LogMessage("Boot launch detected. Presenting initial cinematic lock screen experience.");
            sessionManager.LaunchLockExperience(settings, lockOnDismiss: false);
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
        _lockSessionManager?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
