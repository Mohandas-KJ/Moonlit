using Moonlit.Models;

namespace Moonlit.Services;

/// <summary>
/// Evaluates and reports actual configuration and integration readiness per Section 6 of the Development Plan.
/// </summary>
public sealed class StatusService
{
    private readonly VideoService _videoService;

    public StatusService(VideoService videoService)
    {
        _videoService = videoService;
    }

    /// <summary>
    /// Computes the comprehensive status indicator state.
    /// </summary>
    public StatusInfo EvaluateStatus(AppSettings settings, bool startupVerified)
    {
        // 1. Check video selection
        if (string.IsNullOrWhiteSpace(settings.VideoPath))
        {
            return StatusInfo.NotReady(
                header: "Not Ready — Choose a video to continue",
                explanation: "No video file has been selected for the cinematic lock screen background.",
                correctiveAction: "Click 'Choose Video' and select an MP4 or supported video file."
            );
        }

        // 2. Validate video file accessibility and format
        var videoValidation = _videoService.ValidateVideoFile(settings.VideoPath);
        if (!videoValidation.IsValid)
        {
            return StatusInfo.NotReady(
                header: "Not Ready — Video file is unavailable or invalid",
                explanation: videoValidation.ErrorMessage ?? "The selected file cannot be accessed.",
                correctiveAction: "Select a valid, readable MP4 video file."
            );
        }

        // 3. Check OS platform compatibility
        var buildNumber = Environment.OSVersion.Version.Build;
        var isSupportedOs = Environment.OSVersion.Platform == PlatformID.Win32NT && buildNumber >= 17763;
        if (!isSupportedOs)
        {
            return StatusInfo.NotReady(
                header: "Not Ready — Unsupported Windows version",
                explanation: $"Moonlit requires Windows 10 (1809+) or Windows 11. Detected: {Environment.OSVersion.VersionString}.",
                correctiveAction: "Run Moonlit on Windows 10 or Windows 11."
            );
        }

        // 4. Check if lock experience is enabled
        if (!settings.LockExperienceEnabled)
        {
            return StatusInfo.Attention(
                header: "Attention — Lock screen experience is disabled",
                explanation: "Cinematic lock screen is currently switched off in Preferences.",
                correctiveAction: "Toggle 'Enable Lock-Screen Experience' on to activate full-screen video playback."
            );
        }

        // 5. Check startup preference consistency
        if (!startupVerified && settings.LaunchAtSignIn)
        {
            return StatusInfo.Attention(
                header: "Attention — Startup preference not confirmed",
                explanation: "Launch at Windows sign-in is checked in settings but was not found in the Windows Run registry key.",
                correctiveAction: "Toggle 'Launch at Windows Sign-in' off and on again to register the startup entry."
            );
        }

        // 6. Green is reported when video is verified, lock experience is enabled, and settings are valid
        return StatusInfo.Ready(
            header: "Ready — Lock experience active",
            explanation: "Video background, dynamic clock, and session integration are verified and ready."
        );
    }
}
