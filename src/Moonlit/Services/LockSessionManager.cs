using System.Windows;
using Microsoft.Win32;
using Moonlit.Models;
using Moonlit.Views;

namespace Moonlit.Services;

/// <summary>
/// Coordinates lock and unlock lifecycle events, global keyboard hooks, and manages active LockExperienceWindow instances.
/// </summary>
public sealed class LockSessionManager : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly KeyboardHookService _keyboardHook;
    private LockExperienceWindow? _activeLockWindow;
    private AppSettings _currentSettings;
    private bool _isDisposed;

    public event EventHandler<bool>? LockExperienceStateChanged;

    public bool IsLockExperienceActive => _activeLockWindow != null;

    public KeyboardHookService KeyboardHook => _keyboardHook;

    public LockSessionManager(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _currentSettings = settingsService.LoadSettings();

        // 1. Subscribe to Windows session change notifications (Lock, Unlock, Logon, Logoff)
        SystemEvents.SessionSwitch += OnSessionSwitch;

        // 2. Initialize and hook global keyboard shortcuts (Win + L, Ctrl + Alt + L)
        _keyboardHook = new KeyboardHookService();
        _keyboardHook.LockRequested += OnKeyboardLockRequested;

        if (_currentSettings.LockExperienceEnabled && _currentSettings.InterceptWinL)
        {
            _keyboardHook.StartHook();
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        _currentSettings = settings;

        if (settings.LockExperienceEnabled && settings.InterceptWinL)
        {
            _keyboardHook.StartHook();
        }
        else
        {
            _keyboardHook.StopHook();
        }
    }

    private void OnKeyboardLockRequested(object? sender, EventArgs e)
    {
        _settingsService.LogMessage("Global lock shortcut (Win+L / Ctrl+Alt+L) detected.");

        if (_currentSettings.LockExperienceEnabled)
        {
            LaunchLockExperience(_currentSettings, lockOnDismiss: true);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _settingsService.LogMessage($"Windows SessionSwitch event: {e.Reason}");

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                // Workstation locked
                break;

            case SessionSwitchReason.SessionUnlock:
                // User unlocked / authenticated
                Application.Current.Dispatcher.Invoke(CloseLockExperience);
                break;
        }
    }

    /// <summary>
    /// Launches Moonlit's full-screen cinematic lock experience window.
    /// </summary>
    public void LaunchLockExperience(AppSettings settings, bool lockOnDismiss = true)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_activeLockWindow != null)
            {
                _activeLockWindow.Activate();
                return;
            }

            _activeLockWindow = new LockExperienceWindow();
            _activeLockWindow.Closed += (_, _) =>
            {
                _activeLockWindow = null;
                LockExperienceStateChanged?.Invoke(this, false);
            };

            _activeLockWindow.ConfigureAndLaunch(settings, lockOnDismiss);
            LockExperienceStateChanged?.Invoke(this, true);
            _settingsService.LogMessage("LockExperienceWindow launched.");
        });
    }

    /// <summary>
    /// Closes any running lock experience window and restores application state.
    /// </summary>
    public void CloseLockExperience()
    {
        if (_activeLockWindow != null)
        {
            try
            {
                _activeLockWindow.Close();
            }
            catch
            {
                // Window may already be closing
            }
            finally
            {
                _activeLockWindow = null;
                LockExperienceStateChanged?.Invoke(this, false);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _keyboardHook.LockRequested -= OnKeyboardLockRequested;
        _keyboardHook.Dispose();

        CloseLockExperience();
    }
}
