using System.Text.Json.Serialization;

namespace Moonlit.Models;

/// <summary>
/// Represents persisted user preferences and customization options for Moonlit.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Absolute path to the user-selected video file.
    /// </summary>
    [JsonPropertyName("videoPath")]
    public string? VideoPath { get; set; }

    /// <summary>
    /// Whether the video should loop continuously when played.
    /// </summary>
    [JsonPropertyName("loopPlayback")]
    public bool LoopPlayback { get; set; } = true;

    /// <summary>
    /// Whether the Moonlit application should launch automatically when Windows boots / user signs in.
    /// </summary>
    [JsonPropertyName("launchAtSignIn")]
    public bool LaunchAtSignIn { get; set; } = false;

    /// <summary>
    /// Whether Moonlit should immediately display the cinematic lock screen upon boot.
    /// </summary>
    [JsonPropertyName("startOnBootAsLockScreen")]
    public bool StartOnBootAsLockScreen { get; set; } = true;

    /// <summary>
    /// Whether the Moonlit cinematic lock-screen experience is enabled.
    /// </summary>
    [JsonPropertyName("lockExperienceEnabled")]
    public bool LockExperienceEnabled { get; set; } = true;

    /// <summary>
    /// Whether Moonlit should intercept the Win + L shortcut (and global lock hotkeys like Ctrl + Alt + L)
    /// to display the cinematic lock screen.
    /// </summary>
    [JsonPropertyName("interceptWinL")]
    public bool InterceptWinL { get; set; } = true;

    /// <summary>
    /// Whether to send a simulated keystroke (Space/Enter) immediately upon dismissing Moonlit
    /// so Windows jumps directly to the PIN/Password prompt and avoids the native static clock screen.
    /// </summary>
    [JsonPropertyName("jumpToPasswordPrompt")]
    public bool JumpToPasswordPrompt { get; set; } = true;

    /// <summary>
    /// Whether the digital clock should display time in 24-hour format instead of 12-hour AM/PM format.
    /// </summary>
    [JsonPropertyName("use24HourFormat")]
    public bool Use24HourFormat { get; set; } = false;

    /// <summary>
    /// Whether the digital clock should display seconds.
    /// </summary>
    [JsonPropertyName("showSeconds")]
    public bool ShowSeconds { get; set; } = false;

    /// <summary>
    /// Alignment of the clock and date overlay on the lock screen.
    /// Options: "Center", "BottomLeft", "TopLeft", "BottomRight".
    /// </summary>
    [JsonPropertyName("clockAlignment")]
    public string ClockAlignment { get; set; } = "Center";

    /// <summary>
    /// Opacity of the dark gradient dimming overlay (0.0 to 0.7) to ensure high text readability.
    /// </summary>
    [JsonPropertyName("dimmingOpacity")]
    public double DimmingOpacity { get; set; } = 0.25;

    /// <summary>
    /// Whether to display Moonlit's own interactive cinematic PIN sign-in screen upon waking the lock screen.
    /// </summary>
    [JsonPropertyName("useMoonlitPinScreen")]
    public bool UseMoonlitPinScreen { get; set; } = true;

    /// <summary>
    /// The PIN used to unlock Moonlit and return to the desktop (Defaults to 1080 for testing).
    /// </summary>
    [JsonPropertyName("moonlitPin")]
    public string MoonlitPin { get; set; } = "1080";

    /// <summary>
    /// Timestamp of when settings were last saved.
    /// </summary>
    [JsonPropertyName("lastSaved")]
    public DateTimeOffset? LastSaved { get; set; }

    /// <summary>
    /// Creates a deep clone of the current settings.
    /// </summary>
    public AppSettings Clone() => new()
    {
        VideoPath = VideoPath,
        LoopPlayback = LoopPlayback,
        LaunchAtSignIn = LaunchAtSignIn,
        StartOnBootAsLockScreen = StartOnBootAsLockScreen,
        LockExperienceEnabled = LockExperienceEnabled,
        InterceptWinL = InterceptWinL,
        JumpToPasswordPrompt = JumpToPasswordPrompt,
        UseMoonlitPinScreen = UseMoonlitPinScreen,
        MoonlitPin = MoonlitPin,
        Use24HourFormat = Use24HourFormat,
        ShowSeconds = ShowSeconds,
        ClockAlignment = ClockAlignment,
        DimmingOpacity = DimmingOpacity,
        LastSaved = LastSaved
    };
}
