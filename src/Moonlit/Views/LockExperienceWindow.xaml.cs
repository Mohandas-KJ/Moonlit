using System.Windows;
using System.Windows.Input;
using Moonlit.Integration;
using Moonlit.Models;

namespace Moonlit.Views;

/// <summary>
/// Full-screen cinematic window presenting user-selected video background and dynamic clock overlay.
/// Handles user dismissal and initiates transition to the genuine Windows authentication surface.
/// </summary>
public partial class LockExperienceWindow : Window
{
    private Point? _initialMousePosition;
    private bool _isDismissing;
    private bool _lockOnDismiss;
    private bool _jumpToPasswordPrompt = true;

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

        // Interaction listeners for sign-in dismissal
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

        // Apply Clock settings
        ClockView.Use24HourFormat = settings.Use24HourFormat;
        ClockView.ShowSeconds = settings.ShowSeconds;
        ClockView.ClockAlignment = settings.ClockAlignment;
        ClockView.DimmingOpacity = settings.DimmingOpacity;
        ClockView.ShowDismissHint = true;

        // Apply Video settings
        VideoView.LoopPlayback = settings.LoopPlayback;
        VideoView.LoadVideo(settings.VideoPath, autoPlay: true);

        Show();
        Activate();
        Focus();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Any key dismisses and initiates sign-in transition
        DismissExperience();
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Mouse click dismisses and initiates sign-in transition
        DismissExperience();
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        var currentPosition = e.GetPosition(this);
        if (_initialMousePosition == null)
        {
            _initialMousePosition = currentPosition;
            return;
        }

        // Require significant mouse movement to avoid accidental dismissal by slight desk vibration
        var deltaX = Math.Abs(currentPosition.X - _initialMousePosition.Value.X);
        var deltaY = Math.Abs(currentPosition.Y - _initialMousePosition.Value.Y);

        if (deltaX > 40 || deltaY > 40)
        {
            DismissExperience();
        }
    }

    private void DismissExperience()
    {
        if (_isDismissing) return;
        _isDismissing = true;

        // 1. Stop video playback and release media resources
        VideoView.Stop();
        VideoView.Close();

        // 2. Hide window immediately before locking
        Hide();

        // 3. If configured, trigger the genuine Windows authentication surface (LockWorkStation)
        if (_lockOnDismiss)
        {
            WindowsIntegration.TriggerWindowsLock();

            // 4. Issue 2: Send keystroke to jump directly to password/PIN prompt and skip Windows clock
            if (_jumpToPasswordPrompt)
            {
                Task.Run(async () =>
                {
                    // Delay to allow Winlogon desktop to switch and receive synthetic key input
                    await Task.Delay(200);
                    WindowsIntegration.SendDismissKeystroke();
                });
            }
        }

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        VideoView.Stop();
        VideoView.Close();
        base.OnClosed(e);
    }
}
