using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Moonlit.Integration;
using Moonlit.Models;
using Moonlit.Services;

namespace Moonlit.Views;

/// <summary>
/// Full-screen cinematic window presenting user-selected video background and dynamic clock overlay.
/// In Kiosk Overlay Mode: on keypress or click, smoothly translates the clock upwards, blurs the video,
/// and presents an interactive Moonlit PIN prompt (default "1080"). If idle for 1 minute, reverts to clock/video.
/// </summary>
public partial class LockExperienceWindow : Window
{
    private enum LockViewState
    {
        ClockScreen,
        PinScreen,
        Unlocking
    }

    private bool _isDismissing;
    private bool _lockOnDismiss;
    private bool _jumpToPasswordPrompt = true;
    private bool _useMoonlitPinScreen = true;
    private string _configuredPin = "1080";
    private bool _isAuthorizedClose;

    private LockViewState _currentState = LockViewState.ClockScreen;
    private readonly DispatcherTimer _inactivityTimer;
    private readonly DispatcherTimer _focusGuardTimer;
    private readonly System.Windows.Media.Effects.BlurEffect _videoBlurEffect = new()
    {
        Radius = 0,
        RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
    };

    public LockExperienceWindow()
    {
        InitializeComponent();
        StartupDiagnosticsTracker.RecordWindowCreated();

        // Cover the primary display
        var bounds = WindowsIntegration.GetPrimaryScreenBounds();
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        WindowState = WindowState.Normal;

        // Apply hardware-accelerated blur effect to VideoContainer
        VideoContainer.Effect = _videoBlurEffect;

        // User name display
        var currentUserName = Environment.UserName;
        UserNameText.Text = !string.IsNullOrWhiteSpace(currentUserName) ? currentUserName : "Welcome";

        // Inactivity Timer (1 minute / 60 seconds) to return to clock & video playback
        _inactivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _inactivityTimer.Tick += OnInactivityTimerTick;

        // Focus Guard Timer: Detects Task View / Virtual Desktop switcher theft and restores lock screen
        _focusGuardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _focusGuardTimer.Tick += OnFocusGuardTimerTick;
        _focusGuardTimer.Start();

        // Interaction listeners for clock wakeup, gestures, and activity tracking
        KeyDown += OnWindowKeyDown;
        MouseDown += OnWindowMouseDown;
        MouseMove += OnWindowMouseMove;
        Deactivated += OnWindowDeactivated;
    }

    /// <summary>
    /// Configures the visual elements from the user's settings and starts the cinematic experience.
    /// Prioritizes displaying the window and dynamic clock immediately, then initializes video asynchronously.
    /// </summary>
    public void ConfigureAndLaunch(AppSettings settings, bool lockOnDismiss = false)
    {
        _lockOnDismiss = lockOnDismiss;
        _jumpToPasswordPrompt = settings.JumpToPasswordPrompt;
        _useMoonlitPinScreen = settings.UseMoonlitPinScreen;
        _configuredPin = !string.IsNullOrWhiteSpace(settings.MoonlitPin) ? settings.MoonlitPin : "1080";

        // 1. Reset views and animations
        _currentState = LockViewState.ClockScreen;
        ClockView.ResetClockOffset();
        _videoBlurEffect.Radius = 0;
        PinEntryContainer.Visibility = Visibility.Collapsed;
        PinEntryContainer.Opacity = 0;
        PinTranslateTransform.Y = 40;
        PinTranslateTransform.X = 0;
        PinErrorText.Visibility = Visibility.Collapsed;
        PinInputBox.Password = "";

        // 2. Configure Clock immediately (so it ticks on frame 1)
        ClockView.Use24HourFormat = settings.Use24HourFormat;
        ClockView.ShowSeconds = settings.ShowSeconds;
        ClockView.ClockAlignment = settings.ClockAlignment;
        ClockView.DimmingOpacity = settings.DimmingOpacity;
        ClockView.ShowDismissHint = true;

        // 3. DISPLAY WINDOW IMMEDIATELY! (Before video file I/O & decoder loading)
        Show();
        Activate();
        Focus();
        StartupDiagnosticsTracker.RecordWindowFirstDisplayed();

        // 4. Initialize Video Asynchronously without blocking the UI thread
        VideoView.LoopPlayback = settings.LoopPlayback;
        VideoView.IsMuted = settings.IsMuted;
        _ = VideoView.LoadVideoAsync(settings.VideoPath, autoPlay: true);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_currentState == LockViewState.ClockScreen)
        {
            if (_useMoonlitPinScreen)
            {
                TransitionToPinScreen(e.Key);
                e.Handled = true;
            }
            else
            {
                DismissExperience();
            }
        }
        else if (_currentState == LockViewState.PinScreen)
        {
            ResetInactivityTimer();
            if (e.Key == Key.Escape)
            {
                TransitionToClockScreen();
                e.Handled = true;
            }
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentState == LockViewState.ClockScreen)
        {
            if (_useMoonlitPinScreen)
            {
                TransitionToPinScreen();
            }
            else
            {
                DismissExperience();
            }
        }
        else if (_currentState == LockViewState.PinScreen)
        {
            ResetInactivityTimer();
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        // Mouse pointer can move freely across the screen without rising the curtain on ClockScreen.
        // Only an explicit mouse button press (MouseDown) or key press (KeyDown) will rise the curtain.
        if (_currentState == LockViewState.PinScreen)
        {
            ResetInactivityTimer();
        }
    }

    /// <summary>
    /// Smoothly transitions the clock upwards, applies Gaussian blur to the video,
    /// and reveals Moonlit's interactive PIN entry card.
    /// </summary>
    private void TransitionToPinScreen(Key? initialKey = null)
    {
        if (_currentState != LockViewState.ClockScreen) return;
        _currentState = LockViewState.PinScreen;
        ResetInactivityTimer();

        // 1. Smoothly translate only the Clock & Date text UP (keeps dimming & vignette stationary full-screen)
        ClockView.AnimateClockOffset(
            targetY: -140,
            duration: TimeSpan.FromMilliseconds(380),
            easing: new CubicEase { EasingMode = EasingMode.EaseOut }
        );
        ClockView.AnimateHintOpacity(0, TimeSpan.FromMilliseconds(200));

        // 2. Smoothly Blur the Video Background
        var blurAnimation = new DoubleAnimation
        {
            To = 28,
            Duration = TimeSpan.FromMilliseconds(380),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _videoBlurEffect.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, blurAnimation);

        // 3. Reveal and slide up the PIN entry container
        PinEntryContainer.Visibility = Visibility.Visible;
        PinErrorText.Visibility = Visibility.Collapsed;
        PinInputBox.Password = "";

        var pinFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        PinEntryContainer.BeginAnimation(OpacityProperty, pinFadeIn);

        var pinSlideUp = new DoubleAnimation
        {
            From = 40,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(380),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PinTranslateTransform.BeginAnimation(TranslateTransform.YProperty, pinSlideUp);

        // Pre-populate first character if typed on wakeup
        if (initialKey.HasValue)
        {
            if (initialKey >= Key.D0 && initialKey <= Key.D9)
            {
                PinInputBox.Password = ((char)('0' + (initialKey.Value - Key.D0))).ToString();
            }
            else if (initialKey >= Key.NumPad0 && initialKey <= Key.NumPad9)
            {
                PinInputBox.Password = ((char)('0' + (initialKey.Value - Key.NumPad0))).ToString();
            }
        }

        PinInputBox.Focus();
    }

    /// <summary>
    /// Smoothly slides the clock back down, removes video blur, and resumes pure cinematic playback.
    /// </summary>
    private void TransitionToClockScreen()
    {
        if (_currentState != LockViewState.PinScreen) return;
        _currentState = LockViewState.ClockScreen;
        _inactivityTimer.Stop();

        // 1. Fade out and slide down the PIN card
        var pinFadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
        var pinSlideDown = new DoubleAnimation
        {
            To = 40,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        pinFadeOut.Completed += (_, _) =>
        {
            if (_currentState == LockViewState.ClockScreen)
            {
                PinEntryContainer.Visibility = Visibility.Collapsed;
                PinInputBox.Password = "";
                PinErrorText.Visibility = Visibility.Collapsed;
            }
        };

        PinEntryContainer.BeginAnimation(OpacityProperty, pinFadeOut);
        PinTranslateTransform.BeginAnimation(TranslateTransform.YProperty, pinSlideDown);

        // 2. Smoothly translate Clock back DOWN
        ClockView.AnimateClockOffset(
            targetY: 0,
            duration: TimeSpan.FromMilliseconds(550),
            easing: new CubicEase { EasingMode = EasingMode.EaseInOut }
        );
        ClockView.AnimateHintOpacity(1, TimeSpan.FromMilliseconds(400));

        // 3. Smoothly Unblur the Video Background
        var unblurAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(550),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        _videoBlurEffect.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, unblurAnimation);
    }

    private void ResetInactivityTimer()
    {
        _inactivityTimer.Stop();
        if (_currentState == LockViewState.PinScreen)
        {
            _inactivityTimer.Start();
        }
    }

    private void OnInactivityTimerTick(object? sender, EventArgs e)
    {
        _inactivityTimer.Stop();
        // 1 minute idle on PIN screen reached: smoothly revert to clock and video playback
        TransitionToClockScreen();
    }

    private void OnPinPasswordChanged(object sender, RoutedEventArgs e)
    {
        ResetInactivityTimer();

        // Auto-validate as soon as 4 digits are typed (Mac / iPhone style)
        if (PinInputBox.Password.Length == 4)
        {
            ValidatePin();
        }
    }

    private void OnPinInputKeyDown(object sender, KeyEventArgs e)
    {
        ResetInactivityTimer();

        if (e.Key == Key.Enter)
        {
            ValidatePin();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            TransitionToClockScreen();
            e.Handled = true;
        }
    }

    private void OnUnlockButtonClicked(object sender, RoutedEventArgs e)
    {
        ValidatePin();
    }

    private void ValidatePin()
    {
        if (_currentState != LockViewState.PinScreen) return;

        var enteredPin = PinInputBox.Password;

        if (string.Equals(enteredPin, _configuredPin, StringComparison.Ordinal))
        {
            // PIN Match! Proceed to desktop
            UnlockSuccess();
        }
        else
        {
            // PIN Failed! Show error and shake animation
            TriggerPinFailureFeedback();
        }
    }

    private void UnlockSuccess()
    {
        _currentState = LockViewState.Unlocking;
        _inactivityTimer.Stop();
        _focusGuardTimer.Stop();

        // Smooth fade out to reveal the desktop
        var unlockFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        unlockFade.Completed += (_, _) =>
        {
            _isAuthorizedClose = true;
            StopAndMutePlayback();
            Close();
        };

        BeginAnimation(OpacityProperty, unlockFade);
    }

    private void TriggerPinFailureFeedback()
    {
        PinErrorText.Visibility = Visibility.Visible;

        // 1. Horizontal Shake Animation on the PIN card
        var shakeAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(350)
        };
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350))));

        PinTranslateTransform.BeginAnimation(TranslateTransform.XProperty, shakeAnim);

        // 2. Pulse border brush to red
        var errorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x73, 0x7B));
        PinCardBorder.BorderBrush = errorBrush;

        var colorRevert = new ColorAnimation(
            Color.FromRgb(0x38, 0x43, 0x5C),
            TimeSpan.FromMilliseconds(800)
        );
        errorBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorRevert);

        // Clear input and focus for retry
        PinInputBox.Password = "";
        PinInputBox.Focus();
    }

    public void StopAndMutePlayback()
    {
        try
        {
            VideoView.StopAndMute();
        }
        catch
        {
            // Ignore if already stopped
        }
    }

    /// <summary>
    /// Programmatically closes the lock experience window with authorization.
    /// </summary>
    public void AuthorizeAndClose()
    {
        _isAuthorizedClose = true;
        _focusGuardTimer.Stop();
        _inactivityTimer.Stop();
        StopAndMutePlayback();
        Close();
    }

    private void DismissExperience()
    {
        if (_isDismissing) return;
        _isDismissing = true;
        _inactivityTimer.Stop();
        _focusGuardTimer.Stop();

        // 1. Stop video playback and release media resources immediately to prevent audio bleed
        StopAndMutePlayback();

        // 2. Hide window immediately before locking
        Hide();

        // 3. If configured, trigger the genuine Windows authentication surface (LockWorkStation)
        if (_lockOnDismiss)
        {
            WindowsIntegration.TriggerWindowsLock();

            if (_jumpToPasswordPrompt)
            {
                WindowsIntegration.StartDismissPulseSequence();
            }
        }

        _isAuthorizedClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Prevent unauthorized close (such as user clicking [X] in Task View or sending WM_CLOSE)
        if (!_isAuthorizedClose)
        {
            e.Cancel = true;
            DismissTaskViewAndShellOverlays();
            Topmost = false;
            Topmost = true;
            Activate();
            Focus();
            return;
        }

        _focusGuardTimer.Stop();
        _inactivityTimer.Stop();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _inactivityTimer.Stop();
        _focusGuardTimer.Stop();
        StopAndMutePlayback();
        base.OnClosed(e);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_currentState != LockViewState.Unlocking && !_isAuthorizedClose)
        {
            // If Task View or an external window attempts to steal activation,
            // immediately dismiss the shell switcher and regain foreground focus.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentState != LockViewState.Unlocking && !_isAuthorizedClose)
                {
                    DismissTaskViewAndShellOverlays();
                    Topmost = false;
                    Topmost = true;
                    Activate();
                    Focus();

                    var helper = new WindowInteropHelper(this);
                    if (helper.Handle != IntPtr.Zero)
                    {
                        SetForegroundWindow(helper.Handle);
                        BringWindowToTop(helper.Handle);
                    }
                }
            }), DispatcherPriority.Send);
        }
    }

    private void OnFocusGuardTimerTick(object? sender, EventArgs e)
    {
        if (_currentState == LockViewState.Unlocking || _isAuthorizedClose) return;

        var helper = new WindowInteropHelper(this);
        if (helper.Handle == IntPtr.Zero) return;

        var fgWindow = GetForegroundWindow();
        if (fgWindow != helper.Handle && fgWindow != IntPtr.Zero)
        {
            DismissTaskViewAndShellOverlays();
            Topmost = false;
            Topmost = true;
            Activate();
            Focus();
            SetForegroundWindow(helper.Handle);
            BringWindowToTop(helper.Handle);
        }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;

    private const int VK_ESCAPE = 0x1B;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // 1. Mark as WS_EX_TOOLWINDOW: completely hides Moonlit from Task View and Alt+Tab switchers
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);

            // 2. Disable system menu close item
            var hMenu = GetSystemMenu(hwnd, false);
            if (hMenu != IntPtr.Zero)
            {
                EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_DISABLED | MF_GRAYED);
            }

            // 3. Attach Win32 WndProc message hook to swallow WM_CLOSE and SC_CLOSE
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_CLOSE = 0x0010;
        const int WM_SYSCOMMAND = 0x0112;

        if (!_isAuthorizedClose)
        {
            if (msg == WM_CLOSE)
            {
                handled = true;
                Activate();
                Focus();
                return IntPtr.Zero;
            }

            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_CLOSE)
            {
                handled = true;
                Activate();
                Focus();
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private static void DismissTaskViewAndShellOverlays()
    {
        try
        {
            keybd_event((byte)VK_ESCAPE, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
    }
}
