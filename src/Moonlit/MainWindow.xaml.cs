using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Moonlit.Integration;
using Moonlit.Models;
using Moonlit.Services;

namespace Moonlit;

/// <summary>
/// Interaction logic for MainWindow.xaml — Moonlit Settings & Atmosphere Dashboard
/// </summary>
public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly VideoService _videoService;
    private readonly StartupService _startupService;
    private readonly LockSessionManager _lockSessionManager;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly StatusService _statusService;

    private AppSettings _currentSettings;
    private bool _isPreviewPlaying;
    private bool _isInitializing = true;
    private readonly DispatcherTimer _feedbackTimer;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = App.SettingsService;
        _videoService = new VideoService();
        _startupService = new StartupService();
        _lockSessionManager = App.LockSessionManager;
        _diagnosticsService = new DiagnosticsService(_videoService, _settingsService, _startupService);
        _statusService = new StatusService(_videoService);

        _currentSettings = new AppSettings();

        _feedbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _feedbackTimer.Tick += (_, _) =>
        {
            FooterFeedbackText.Visibility = Visibility.Collapsed;
            _feedbackTimer.Stop();
        };

        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        LoadAndApplySettings();
        _isInitializing = false;
        RefreshStatus();
    }

    private void LoadAndApplySettings()
    {
        _currentSettings = _settingsService.LoadSettings();

        // 1. Video Path
        if (!string.IsNullOrWhiteSpace(_currentSettings.VideoPath))
        {
            ApplyVideoSelection(_currentSettings.VideoPath, autoPlayPreview: false);
        }
        else
        {
            SelectedFilePathText.Text = "No video selected";
            SelectedFilePathText.Foreground = (Brush)FindResource("MutedTextBrush");
            PreviewToggleButton.IsEnabled = false;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            FileInfoPanel.Visibility = Visibility.Collapsed;
        }

        // 2. Clock & Date Options
        Format24HourCheckBox.IsChecked = _currentSettings.Use24HourFormat;
        ShowSecondsCheckBox.IsChecked = _currentSettings.ShowSeconds;

        ClockPreviewOverlay.Use24HourFormat = _currentSettings.Use24HourFormat;
        ClockPreviewOverlay.ShowSeconds = _currentSettings.ShowSeconds;
        ClockPreviewOverlay.ClockAlignment = _currentSettings.ClockAlignment;
        ClockPreviewOverlay.DimmingOpacity = _currentSettings.DimmingOpacity;

        SelectAlignmentItem(_currentSettings.ClockAlignment);
        DimmingSlider.Value = _currentSettings.DimmingOpacity * 100.0;
        DimmingValueText.Text = $"{Math.Round(DimmingSlider.Value)}%";

        // 3. Lock Integration Preferences
        InterceptWinLCheckBox.IsChecked = _currentSettings.InterceptWinL;
        JumpToPasswordPromptCheckBox.IsChecked = _currentSettings.JumpToPasswordPrompt;
        StartOnBootCheckBox.IsChecked = _currentSettings.StartOnBootAsLockScreen;
        LockExperienceCheckBox.IsChecked = _currentSettings.LockExperienceEnabled;
        LoopCheckBox.IsChecked = _currentSettings.LoopPlayback;

        UpdateWinLStatusText();

        // 4. Startup Registry Check
        var isActuallyEnabled = _startupService.IsStartupEnabled();
        StartupCheckBox.IsChecked = isActuallyEnabled;
        _currentSettings.LaunchAtSignIn = isActuallyEnabled;

        if (isActuallyEnabled)
        {
            StartupStatusHintText.Text = "Verified in Windows Startup registry.";
            StartupStatusHintText.Foreground = (Brush)FindResource("ReadyBrush");
        }
        else
        {
            StartupStatusHintText.Text = "Starts only when manually opened.";
            StartupStatusHintText.Foreground = (Brush)FindResource("SecondaryTextBrush");
        }

        // Inform background session manager of loaded settings
        _lockSessionManager.UpdateSettings(_currentSettings);
    }

    private void UpdateWinLStatusText()
    {
        var isWinLConfigured = WindowsIntegration.IsLockWorkstationDisabled();
        var isNoLockScreenConfigured = WindowsIntegration.IsNoLockScreenConfigured();

        // 1. Win+L policy indicator
        if (isWinLConfigured)
        {
            WinLPolicyStatusLabel.Text = "Configured (Active)";
            WinLPolicyStatusLabel.Foreground = (Brush)FindResource("ReadyBrush");
        }
        else
        {
            WinLPolicyStatusLabel.Text = "Not Configured";
            WinLPolicyStatusLabel.Foreground = (Brush)FindResource("WarningBrush");
        }

        // 2. NoLockScreen indicator
        if (isNoLockScreenConfigured)
        {
            NoLockScreenStatusLabel.Text = "Configured (Bypassing Curtain)";
            NoLockScreenStatusLabel.Foreground = (Brush)FindResource("ReadyBrush");
        }
        else
        {
            NoLockScreenStatusLabel.Text = "Not Configured";
            NoLockScreenStatusLabel.Foreground = (Brush)FindResource("WarningBrush");
        }

        // 3. Hotkey status text
        if (isWinLConfigured)
        {
            WinLStatusText.Text = "Active: Win+L and Ctrl+Alt+L directly launch Moonlit.";
            WinLStatusText.Foreground = (Brush)FindResource("ReadyBrush");
        }
        else
        {
            WinLStatusText.Text = "Active for Ctrl+Alt+L, Win+Alt+L, and Ctrl+Shift+L. (Click 1-Click Setup to enable Win+L).";
            WinLStatusText.Foreground = (Brush)FindResource("SecondaryTextBrush");
        }
    }

    private void OnConfigureAllPoliciesClicked(object sender, RoutedEventArgs e)
    {
        var success = WindowsIntegration.ConfigurePoliciesElevated();
        UpdateWinLStatusText();
        RefreshStatus();

        if (success)
        {
            ShowTemporaryFeedback("System policies configured! Win+L and curtain bypass are now active.");
            MessageBox.Show(
                "Moonlit system integration policies have been successfully configured!\n\n" +
                "• Win+L is now directly routed to Moonlit.\n" +
                "• The static Windows clock curtain is bypassed to jump straight to your PIN/password prompt.\n" +
                "• Full permissions have been granted to toggle locking seamlessly.",
                "System Integration Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            MessageBox.Show(
                "Unable to apply system policies. Administrator privileges were cancelled or restricted.",
                "Policy Notice",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void OnRevertPoliciesClicked(object sender, RoutedEventArgs e)
    {
        var (success, message) = WindowsIntegration.ClearAllMoonlitRegistryPolicies();
        UpdateWinLStatusText();
        RefreshStatus();

        if (success)
        {
            ShowTemporaryFeedback("Windows 11 defaults restored! All Moonlit registry policies cleared.");
            MessageBox.Show(
                "Windows 11 Defaults Fully Restored!\n\n" +
                "• All Moonlit registry entries (DisableLockWorkstation, NoLockScreen, DoNotDisplayLockScreen) have been removed.\n" +
                "• Windows default lock screen curtain and widgets are restored.\n" +
                "• Standard Windows Win+L lock behavior is restored.",
                "Windows Defaults Restored",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            MessageBox.Show(
                $"Notice: {message}",
                "Registry Restoration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void OnCreateShortcutsClicked(object sender, RoutedEventArgs e)
    {
        var success = StartupService.CreateLockShortcuts();
        if (success)
        {
            ShowTemporaryFeedback("Created 'Lock PC with Moonlit' shortcuts on Desktop & Start Menu!");
            MessageBox.Show(
                "Shortcuts successfully created!\n\n" +
                "• 'Lock PC with Moonlit' is now on your Desktop.\n" +
                "• 'Lock PC with Moonlit' is now in your Start Menu Programs.\n\n" +
                "You can click either shortcut anytime to instantly display your cinematic lock screen.",
                "Shortcuts Created",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            ShowTemporaryFeedback("Failed to create shortcuts.");
        }
    }

    private void OnOpenAutoLogonClicked(object sender, RoutedEventArgs e)
    {
        var success = WindowsIntegration.OpenAutoLogonUtility();
        if (success)
        {
            ShowTemporaryFeedback("Opened Windows Netplwiz (User Accounts).");
            MessageBox.Show(
                "Windows Netplwiz (User Accounts) has opened.\n\n" +
                "To have your PC boot directly into Moonlit's cinematic lock screen:\n" +
                "1. Select your user account.\n" +
                "2. Uncheck 'Users must enter a user name and password to use this computer'.\n" +
                "3. Click 'Apply' and confirm your password.\n\n" +
                "When your PC powers on, it will boot directly into Moonlit, and touching any key/mouse will require your PIN/Password to unlock!",
                "Auto-Logon Guidance",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            ShowTemporaryFeedback("Unable to launch Netplwiz.");
        }
    }

    private void ApplyVideoSelection(string filePath, bool autoPlayPreview)
    {
        var validation = _videoService.ValidateVideoFile(filePath);
        SelectedFilePathText.Text = filePath;

        if (validation.IsValid)
        {
            _currentSettings.VideoPath = filePath;
            SelectedFilePathText.Foreground = (Brush)FindResource("PrimaryTextBrush");
            PreviewToggleButton.IsEnabled = true;
            FileInfoPanel.Visibility = Visibility.Visible;
            FileSizeText.Text = VideoService.FormatFileSize(validation.FileSizeBytes);
            FileFormatText.Text = Path.GetExtension(filePath).ToUpperInvariant();

            try
            {
                PreviewPlayer.Source = new Uri(filePath, UriKind.Absolute);
                PreviewPlaceholder.Visibility = Visibility.Collapsed;

                if (autoPlayPreview)
                {
                    StartPlayback();
                }
            }
            catch (Exception ex)
            {
                _settingsService.LogMessage($"Preview load error: {ex.Message}");
            }
        }
        else
        {
            SelectedFilePathText.Foreground = (Brush)FindResource("ErrorBrush");
            PreviewToggleButton.IsEnabled = false;
            FileInfoPanel.Visibility = Visibility.Collapsed;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            StopPlayback();
        }
    }

    private void OnChooseVideoClicked(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Video for Cinematic Lock Screen Background",
            Filter = VideoService.FileDialogFilter,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog(this) == true)
        {
            ApplyVideoSelection(openFileDialog.FileName, autoPlayPreview: true);
            RefreshStatus();
            ShowTemporaryFeedback("Video selected.");
        }
    }

    private void OnPreviewToggleClicked(object sender, RoutedEventArgs e)
    {
        if (_isPreviewPlaying)
        {
            PausePlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    private void StartPlayback()
    {
        if (PreviewPlayer.Source == null) return;
        PreviewPlayer.Play();
        _isPreviewPlaying = true;
        PreviewToggleButton.Content = "Pause";
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void PausePlayback()
    {
        PreviewPlayer.Pause();
        _isPreviewPlaying = false;
        PreviewToggleButton.Content = "Play Preview";
    }

    private void StopPlayback()
    {
        PreviewPlayer.Stop();
        _isPreviewPlaying = false;
        PreviewToggleButton.Content = "Play Preview";
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (LoopCheckBox.IsChecked == true)
        {
            PreviewPlayer.Position = TimeSpan.Zero;
            PreviewPlayer.Play();
        }
        else
        {
            StopPlayback();
        }
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _settingsService.LogMessage($"Media preview failed: {e.ErrorException?.Message}");
        StopPlayback();
        PreviewPlaceholder.Visibility = Visibility.Visible;
        MessageBox.Show(
            $"Unable to preview media:\n{e.ErrorException?.Message}",
            "Playback Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
    }

    private void OnClockOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _currentSettings.Use24HourFormat = Format24HourCheckBox.IsChecked ?? false;
        _currentSettings.ShowSeconds = ShowSecondsCheckBox.IsChecked ?? false;

        ClockPreviewOverlay.Use24HourFormat = _currentSettings.Use24HourFormat;
        ClockPreviewOverlay.ShowSeconds = _currentSettings.ShowSeconds;
    }

    private void OnAlignmentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (AlignmentComboBox.SelectedItem is ComboBoxItem item)
        {
            var alignmentName = item.Content.ToString()?.Replace("-", "") ?? "Center";
            _currentSettings.ClockAlignment = alignmentName;
            ClockPreviewOverlay.ClockAlignment = alignmentName;
        }
    }

    private void SelectAlignmentItem(string alignment)
    {
        var targetText = alignment switch
        {
            "BottomLeft" => "Bottom-Left",
            "TopLeft" => "Top-Left",
            "BottomRight" => "Bottom-Right",
            _ => "Center"
        };

        foreach (var item in AlignmentComboBox.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Content.ToString() == targetText)
            {
                AlignmentComboBox.SelectedItem = cbi;
                break;
            }
        }
    }

    private void OnDimmingSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;

        var fraction = e.NewValue / 100.0;
        _currentSettings.DimmingOpacity = fraction;
        DimmingValueText.Text = $"{Math.Round(e.NewValue)}%";
        ClockPreviewOverlay.DimmingOpacity = fraction;
    }

    private void OnPreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _currentSettings.LockExperienceEnabled = LockExperienceCheckBox.IsChecked ?? true;
        _currentSettings.LoopPlayback = LoopCheckBox.IsChecked ?? true;
        _currentSettings.InterceptWinL = InterceptWinLCheckBox.IsChecked ?? true;
        _currentSettings.JumpToPasswordPrompt = JumpToPasswordPromptCheckBox.IsChecked ?? true;
        _currentSettings.StartOnBootAsLockScreen = StartOnBootCheckBox.IsChecked ?? true;

        _lockSessionManager.UpdateSettings(_currentSettings);
        RefreshStatus();
    }

    private void OnStartupCheckboxToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var shouldEnable = StartupCheckBox.IsChecked ?? false;
        var result = _startupService.SetStartupEnabled(shouldEnable);

        _currentSettings.LaunchAtSignIn = result.IsEnabled;
        StartupStatusHintText.Text = result.Message;
        StartupStatusHintText.Foreground = result.Success
            ? (Brush)FindResource("ReadyBrush")
            : (Brush)FindResource("ErrorBrush");

        _settingsService.LogMessage($"Startup setting toggled: {result.Message}");
        RefreshStatus();
    }

    private void OnTestLockExperienceClicked(object sender, RoutedEventArgs e)
    {
        // Save current preferences before testing
        SyncCurrentSettings();

        // Launch in test mode: dismisses back to desktop without locking workstation
        _lockSessionManager.LaunchLockExperience(_currentSettings, lockOnDismiss: false);
    }

    private void OnLockPcClicked(object sender, RoutedEventArgs e)
    {
        // Save current preferences before locking
        SyncCurrentSettings();

        // Launch in live lock mode: dismisses into genuine Windows lock screen (PIN/Hello)
        _lockSessionManager.LaunchLockExperience(_currentSettings, lockOnDismiss: true);
    }

    private async void OnSaveSettingsClicked(object sender, RoutedEventArgs e)
    {
        SyncCurrentSettings();

        var success = await _settingsService.SaveSettingsAsync(_currentSettings);
        if (success)
        {
            _lockSessionManager.UpdateSettings(_currentSettings);
            ShowTemporaryFeedback("Settings saved successfully.");
            RefreshStatus();
        }
        else
        {
            MessageBox.Show(
                "Failed to save settings. Please inspect application logs.",
                "Save Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void SyncCurrentSettings()
    {
        _currentSettings.VideoPath = SelectedFilePathText.Text != "No video selected" ? SelectedFilePathText.Text : null;
        _currentSettings.LoopPlayback = LoopCheckBox.IsChecked ?? true;
        _currentSettings.LockExperienceEnabled = LockExperienceCheckBox.IsChecked ?? true;
        _currentSettings.InterceptWinL = InterceptWinLCheckBox.IsChecked ?? true;
        _currentSettings.JumpToPasswordPrompt = JumpToPasswordPromptCheckBox.IsChecked ?? true;
        _currentSettings.StartOnBootAsLockScreen = StartOnBootCheckBox.IsChecked ?? true;
        _currentSettings.LaunchAtSignIn = StartupCheckBox.IsChecked ?? false;
        _currentSettings.Use24HourFormat = Format24HourCheckBox.IsChecked ?? false;
        _currentSettings.ShowSeconds = ShowSecondsCheckBox.IsChecked ?? false;
        _currentSettings.DimmingOpacity = DimmingSlider.Value / 100.0;

        if (AlignmentComboBox.SelectedItem is ComboBoxItem item)
        {
            _currentSettings.ClockAlignment = item.Content.ToString()?.Replace("-", "") ?? "Center";
        }
    }

    private void OnRefreshStatusClicked(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        UpdateWinLStatusText();
        ShowTemporaryFeedback("Status refreshed.");
    }

    private void RefreshStatus()
    {
        var isStartupConfirmed = _startupService.IsStartupEnabled();
        var status = _statusService.EvaluateStatus(_currentSettings, isStartupConfirmed);

        // Update status badge
        switch (status.Level)
        {
            case StatusLevel.Ready:
                StatusBadgeBorder.Background = (Brush)FindResource("ReadyBgBrush");
                StatusBadgeBorder.BorderBrush = (Brush)FindResource("ReadyBrush");
                StatusIndicatorDot.Fill = (Brush)FindResource("ReadyBrush");
                DetailStatusDot.Fill = (Brush)FindResource("ReadyBrush");
                StatusBadgeText.Text = "Ready";
                break;

            case StatusLevel.Attention:
                StatusBadgeBorder.Background = (Brush)FindResource("WarningBgBrush");
                StatusBadgeBorder.BorderBrush = (Brush)FindResource("WarningBrush");
                StatusIndicatorDot.Fill = (Brush)FindResource("WarningBrush");
                DetailStatusDot.Fill = (Brush)FindResource("WarningBrush");
                StatusBadgeText.Text = "Attention";
                break;

            case StatusLevel.NotReady:
            default:
                StatusBadgeBorder.Background = (Brush)FindResource("ErrorBgBrush");
                StatusBadgeBorder.BorderBrush = (Brush)FindResource("ErrorBrush");
                StatusIndicatorDot.Fill = (Brush)FindResource("ErrorBrush");
                DetailStatusDot.Fill = (Brush)FindResource("ErrorBrush");
                StatusBadgeText.Text = "Not Ready";
                break;
        }

        StatusHeadingText.Text = status.Header;
        StatusExplanationText.Text = status.Explanation;

        if (!string.IsNullOrWhiteSpace(status.CorrectiveAction))
        {
            CorrectiveActionBorder.Visibility = Visibility.Visible;
            CorrectiveActionText.Text = status.CorrectiveAction;
        }
        else
        {
            CorrectiveActionBorder.Visibility = Visibility.Collapsed;
        }

        // Diagnostics expander
        var diag = _diagnosticsService.RunDiagnostics(_currentSettings);
        DiagnosticDetailsText.Text =
            $"OS Version:              {diag.OsVersion} (Build {diag.BuildNumber})\n" +
            $"Primary Display:         {diag.DisplayWidth} x {diag.DisplayHeight} px\n" +
            $"Video Configured:        {diag.IsVideoConfigured}\n" +
            $"Video Accessible:        {diag.IsVideoAccessible}\n" +
            $"Lock Experience Enabled: {diag.IsLockExperienceEnabled}\n" +
            $"Win+L Intercept Hook:    {_currentSettings.InterceptWinL}\n" +
            $"Win+L Registry Policy:   {(diag.IsWinLPolicyActive ? "Configured (Active)" : "Not Set (Elevated setup required)")}\n" +
            $"NoLockScreen Policy:     {(diag.IsNoLockScreenPolicyActive ? "Configured (Curtain bypassed)" : "Not Set")}\n" +
            $"Jump to Password Prompt: {_currentSettings.JumpToPasswordPrompt}\n" +
            $"Startup at Sign-in:      {diag.IsStartupEnabled}\n" +
            $"Settings File:           {diag.SettingsPath}\n" +
            $"Logs Directory:          {diag.LogsPath}\n" +
            $"\nSummary:\n{diag.Summary}";
    }

    private void ShowTemporaryFeedback(string message)
    {
        FooterFeedbackText.Text = message;
        FooterFeedbackText.Visibility = Visibility.Visible;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    private void OnExitAppClicked(object sender, RoutedEventArgs e)
    {
        App.ExitApplication();
    }

    protected override void OnClosed(EventArgs e)
    {
        _feedbackTimer.Stop();
        PreviewPlayer.Close();

        _settingsService.LogMessage("Dashboard window closed. Moonlit remains active in background and accessible via system tray.");
        App.TrayIconService?.ShowBalloon("Moonlit", "Moonlit is active in the background. Click the tray icon anytime to reopen settings.");

        base.OnClosed(e);
    }
}
