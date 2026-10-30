using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Moonlit.Integration;
using Moonlit.Models;

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

    private Point? _initialMousePosition;
    private bool _isDismissing;
    private bool _lockOnDismiss;
    private bool _jumpToPasswordPrompt = true;
    private bool _useMoonlitPinScreen = true;
    private string _configuredPin = "1080";

    private LockViewState _currentState = LockViewState.ClockScreen;
    private readonly DispatcherTimer _inactivityTimer;
    private readonly System.Windows.Media.Effects.BlurEffect _videoBlurEffect = new()
    {
        Radius = 0,
        RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
    };
    private readonly TranslateTransform _clockTranslateTransform = new();

    public LockExperienceWindow()
    {
        InitializeComponent();

        // Cover the primary display
        var bounds = WindowsIntegration.GetPrimaryScreenBounds();
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        WindowState = WindowState.Normal;

        // Apply hardware-accelerated blur effect to VideoContainer and translation to ClockContainer
        VideoContainer.Effect = _videoBlurEffect;
        ClockContainer.RenderTransform = _clockTranslateTransform;

        // User name display
        var currentUserName = Environment.UserName;
        UserNameText.Text = !string.IsNullOrWhiteSpace(currentUserName) ? currentUserName : "Welcome";

        // Inactivity Timer (1 minute / 60 seconds) to return to clock & video playback
        _inactivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _inactivityTimer.Tick += OnInactivityTimerTick;

        // Interaction listeners for clock wakeup and activity tracking
        KeyDown += OnWindowKeyDown;
        MouseDown += OnWindowMouseDown;
        MouseMove += OnWindowMouseMove;
    }

    /// <summary>
    /// Configures the visual elements from the user's settings and starts the cinematic experience.
    /// </summary>
    public void ConfigureAndLaunch(AppSettings settings, bool lockOnDismiss = false)
    {
        _lockOnDismiss = lockOnDismiss;
        _jumpToPasswordPrompt = settings.JumpToPasswordPrompt;
        _useMoonlitPinScreen = settings.UseMoonlitPinScreen;
        _configuredPin = !string.IsNullOrWhiteSpace(settings.MoonlitPin) ? settings.MoonlitPin : "1080";

        // Apply Clock settings
        ClockView.Use24HourFormat = settings.Use24HourFormat;
        ClockView.ShowSeconds = settings.ShowSeconds;
        ClockView.ClockAlignment = settings.ClockAlignment;
        ClockView.DimmingOpacity = settings.DimmingOpacity;
        ClockView.ShowDismissHint = true;

        // Apply Video settings
        VideoView.LoopPlayback = settings.LoopPlayback;
        VideoView.LoadVideo(settings.VideoPath, autoPlay: true);

        // Reset views and animations
        _currentState = LockViewState.ClockScreen;
        _clockTranslateTransform.Y = 0;
        _videoBlurEffect.Radius = 0;
        PinEntryContainer.Visibility = Visibility.Collapsed;
        PinEntryContainer.Opacity = 0;
        PinTranslateTransform.Y = 40;
        PinTranslateTransform.X = 0;
        PinErrorText.Visibility = Visibility.Collapsed;
        PinInputBox.Password = "";

        Show();
        Activate();
        Focus();
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
        var currentPosition = e.GetPosition(this);
        if (_initialMousePosition == null)
        {
            _initialMousePosition = currentPosition;
            return;
        }

        if (_currentState == LockViewState.ClockScreen)
        {
            var deltaX = Math.Abs(currentPosition.X - _initialMousePosition.Value.X);
            var deltaY = Math.Abs(currentPosition.Y - _initialMousePosition.Value.Y);

            if (deltaX > 40 || deltaY > 40)
            {
                _initialMousePosition = currentPosition;
                if (_useMoonlitPinScreen)
                {
                    TransitionToPinScreen();
                }
                else
                {
                    DismissExperience();
                }
            }
        }
        else if (_currentState == LockViewState.PinScreen)
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

        // 1. Smoothly translate Clock UP
        var clockUpAnimation = new DoubleAnimation
        {
            To = -140,
            Duration = TimeSpan.FromMilliseconds(380),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _clockTranslateTransform.BeginAnimation(TranslateTransform.YProperty, clockUpAnimation);
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
        var clockDownAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(550),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        _clockTranslateTransform.BeginAnimation(TranslateTransform.YProperty, clockDownAnimation);
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

        // Smooth fade out to reveal the desktop
        var unlockFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        unlockFade.Completed += (_, _) =>
        {
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

    private void DismissExperience()
    {
        if (_isDismissing) return;
        _isDismissing = true;
        _inactivityTimer.Stop();

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

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _inactivityTimer.Stop();
        StopAndMutePlayback();
        base.OnClosed(e);
    }
}
