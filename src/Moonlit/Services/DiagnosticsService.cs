using System.Windows;
using Moonlit.Integration;
using Moonlit.Models;

namespace Moonlit.Services;

/// <summary>
/// Diagnostic information summary about system environment and Moonlit components.
/// </summary>
public sealed record SystemDiagnostics(
    string OsVersion,
    int BuildNumber,
    bool IsSupportedWindowsVersion,
    double DisplayWidth,
    double DisplayHeight,
    bool IsVideoConfigured,
    bool IsVideoAccessible,
    string? VideoError,
    bool IsStartupEnabled,
    bool IsLockExperienceEnabled,
    bool IsWinLPolicyActive,
    bool IsNoLockScreenPolicyActive,
    string SettingsPath,
    string LogsPath,
    string Summary
);

/// <summary>
/// Service responsible for environment diagnostics and integration verification per Section 5.
/// </summary>
public sealed class DiagnosticsService
{
    private readonly VideoService _videoService;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;

    public DiagnosticsService(VideoService videoService, SettingsService settingsService, StartupService startupService)
    {
        _videoService = videoService;
        _settingsService = settingsService;
        _startupService = startupService;
    }

    /// <summary>
    /// Probes system environment, screen metrics, configuration files, and video accessibility.
    /// </summary>
    public SystemDiagnostics RunDiagnostics(AppSettings settings)
    {
        var osVersion = Environment.OSVersion.VersionString;
        var buildNumber = Environment.OSVersion.Version.Build;
        var isSupportedOs = Environment.OSVersion.Platform == PlatformID.Win32NT && buildNumber >= 17763; // Windows 10 1809+

        var bounds = WindowsIntegration.GetPrimaryScreenBounds();

        var isVideoConfigured = !string.IsNullOrWhiteSpace(settings.VideoPath);
        var videoValidation = isVideoConfigured ? _videoService.ValidateVideoFile(settings.VideoPath) : null;
        var isVideoAccessible = videoValidation?.IsValid ?? false;

        var isStartupEnabled = _startupService.IsStartupEnabled();
        var isWinLPolicyActive = WindowsIntegration.IsLockWorkstationDisabled();
        var isNoLockScreenPolicyActive = WindowsIntegration.IsNoLockScreenConfigured();

        var summary = BuildSummary(isSupportedOs, isVideoConfigured, isVideoAccessible, settings.LockExperienceEnabled);

        return new SystemDiagnostics(
            OsVersion: osVersion,
            BuildNumber: buildNumber,
            IsSupportedWindowsVersion: isSupportedOs,
            DisplayWidth: bounds.Width,
            DisplayHeight: bounds.Height,
            IsVideoConfigured: isVideoConfigured,
            IsVideoAccessible: isVideoAccessible,
            VideoError: videoValidation?.ErrorMessage,
            IsStartupEnabled: isStartupEnabled,
            IsLockExperienceEnabled: settings.LockExperienceEnabled,
            IsWinLPolicyActive: isWinLPolicyActive,
            IsNoLockScreenPolicyActive: isNoLockScreenPolicyActive,
            SettingsPath: _settingsService.SettingsFilePath,
            LogsPath: _settingsService.LogsDirectory,
            Summary: summary
        );
    }

    private static string BuildSummary(bool isSupportedOs, bool isVideoConfigured, bool isVideoAccessible, bool isLockEnabled)
    {
        if (!isSupportedOs)
        {
            return "Operating system version is below Windows 10 (1809+).";
        }

        if (!isVideoConfigured)
        {
            return "No video selected. Choose an MP4 video file to complete configuration.";
        }

        if (!isVideoAccessible)
        {
            return "Selected video file is missing or inaccessible.";
        }

        if (!isLockEnabled)
        {
            return "Lock-screen experience is currently paused/disabled in preferences.";
        }

        return "Environment and media verified. Ready to display cinematic lock experience.";
    }
}
