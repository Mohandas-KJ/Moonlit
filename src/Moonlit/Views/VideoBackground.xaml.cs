using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Moonlit.Views;

/// <summary>
/// UserControl hosting video playback with aspect ratio preservation and continuous looping.
/// </summary>
public partial class VideoBackground : UserControl
{
    private string? _currentVideoPath;

    public bool LoopPlayback { get; set; } = true;

    public event EventHandler<string>? PlaybackError;

    public VideoBackground()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Loads and optionally starts playback of the specified video file.
    /// </summary>
    public bool LoadVideo(string? videoPath, bool autoPlay = true)
    {
        _currentVideoPath = videoPath;

        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            ShowFallback("Selected video file is missing or unconfigured.");
            return false;
        }

        try
        {
            Player.Source = new Uri(videoPath, UriKind.Absolute);
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
