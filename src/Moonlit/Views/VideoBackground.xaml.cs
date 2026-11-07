using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Moonlit.Services;

namespace Moonlit.Views;

/// <summary>
/// UserControl hosting video playback with aspect ratio preservation and continuous looping.
/// Shows an instant dark atmospheric canvas immediately on frame 1 and smoothly crossfades
/// into the video as soon as the first frame is ready, avoiding black-screen flashes.
/// </summary>
public partial class VideoBackground : UserControl
{
    private string? _currentVideoPath;
    private bool _isMuted;

    public bool LoopPlayback { get; set; } = true;

    /// <summary>
    /// Gets or sets whether audio playback is muted.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyMuteState();
        }
    }

    public event EventHandler<string>? PlaybackError;

    public VideoBackground()
    {
        InitializeComponent();
    }

    private void ApplyMuteState()
    {
        Player.IsMuted = _isMuted;
        Player.Volume = _isMuted ? 0 : 1;
    }

    /// <summary>
    /// Asynchronously initializes and starts playback of the video without blocking the UI thread.
    /// Keeps atmospheric fallback canvas visible until the first frame is ready.
    /// </summary>
    public async Task<bool> LoadVideoAsync(string? videoPath, bool autoPlay = true)
    {
        _currentVideoPath = videoPath;
        StartupDiagnosticsTracker.RecordVideoInitStarted();

        if (string.IsNullOrWhiteSpace(videoPath))
        {
            ShowFallback("Selected video file is missing or unconfigured.");
            return false;
        }

        // Verify file existence asynchronously on background thread
        var fileExists = await Task.Run(() => File.Exists(videoPath));
        if (!fileExists)
        {
            ShowFallback($"Video file not found: {Path.GetFileName(videoPath)}");
            return false;
        }

        try
        {
            Player.Opacity = 0;
            Player.Source = new Uri(videoPath, UriKind.Absolute);
            ApplyMuteState();
            FallbackOverlay.Visibility = Visibility.Collapsed;

            if (autoPlay)
            {
                Player.Play();
            }
            return true;
        }
        catch (Exception ex)
        {
            ShowFallback($"Playback error: {ex.Message}");
            PlaybackError?.Invoke(this, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Loads and optionally starts playback of the specified video file synchronously.
    /// </summary>
    public bool LoadVideo(string? videoPath, bool autoPlay = true)
    {
        _currentVideoPath = videoPath;
        StartupDiagnosticsTracker.RecordVideoInitStarted();

        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            ShowFallback("Selected video file is missing or unconfigured.");
            return false;
        }

        try
        {
            Player.Opacity = 0;
            Player.Source = new Uri(videoPath, UriKind.Absolute);
            ApplyMuteState();
            FallbackOverlay.Visibility = Visibility.Collapsed;

            if (autoPlay)
            {
                Player.Play();
            }
            return true;
        }
        catch (Exception ex)
        {
            ShowFallback($"Playback error: {ex.Message}");
            PlaybackError?.Invoke(this, ex.Message);
            return false;
        }
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        ApplyMuteState();
        StartupDiagnosticsTracker.RecordFirstVideoFrameDisplayed();

        // Smoothly fade in the video from the atmospheric startup canvas
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
        Player.BeginAnimation(OpacityProperty, fadeIn);
    }

    public void Play()
    {
        if (Player.Source != null)
        {
            Player.Play();
        }
    }

    public void Pause()
    {
        Player.Pause();
    }

    public void Stop()
    {
        Player.Stop();
    }

    public void StopAndMute()
    {
        Player.IsMuted = true;
        Player.Stop();
        Player.Close();
    }

    public void Close()
    {
        Player.Close();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (LoopPlayback)
        {
            ApplyMuteState();
            Player.Position = TimeSpan.Zero;
            Player.Play();
        }
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        var message = e.ErrorException?.Message ?? "Video codec decoding failed.";
        ShowFallback($"Media playback error: {message}");
        PlaybackError?.Invoke(this, message);
    }

    private void ShowFallback(string message)
    {
        Player.Stop();
        FallbackMessageText.Text = message;
        FallbackOverlay.Visibility = Visibility.Visible;
    }
}
