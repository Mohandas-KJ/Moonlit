using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace Moonlit.Integration;

/// <summary>
/// Status of Windows Lock Screen configuration and prerequisite checks.
/// </summary>
public enum LockScreenBackgroundMode
{
    Picture,
    Spotlight,
    Slideshow,
    Unknown
}

/// <summary>
/// Diagnostic report on native Windows lock-screen capabilities and integration status.
/// </summary>
public sealed record LockScreenDiagnostics(
    string OsVersion,
    int BuildNumber,
    bool IsSupportedWindowsVersion,
    LockScreenBackgroundMode BackgroundMode,
    bool HasCacheDirectory,
    bool HasCacheAccess,
    string? UserSid,
    bool FeatureOverrideConfigured,
    bool NativeVideoApiAvailable,
    string Summary
);

/// <summary>
/// Handles native Windows lock-screen environment inspection and diagnostic verification.
/// Adheres strictly to PLAN.md: No fake overlays, no screen-saver substitutes, no silent system tampering.
/// </summary>
public sealed class LockScreenIntegration
{
    public const int Windows11MinBuild = 22000;
    public const int Windows11FeatureChangeBuild = 26200;
    public const string DefaultFeatureOverrideKey = @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\2920700556";

    /// <summary>
    /// Executes a comprehensive, non-destructive diagnostic check of the Windows lock-screen environment.
    /// </summary>
    public LockScreenDiagnostics ProbeEnvironment()
    {
        var osVersion = Environment.OSVersion.VersionString;
        var buildNumber = Environment.OSVersion.Version.Build;
        var isSupportedOs = Environment.OSVersion.Platform == PlatformID.Win32NT && buildNumber >= 17763; // Windows 10 1809+

        var userSid = GetCurrentUserSid();
        var backgroundMode = DetectBackgroundMode();
        var (hasCacheDir, hasCacheAccess) = CheckCacheDirectory(userSid);
        var featureConfigured = CheckFeatureOverride();

        // As documented in docs/FEASIBILITY.md, Windows does not provide a native video lock-screen API.
        const bool nativeVideoApiAvailable = false;

        var summary = BuildSummaryMessage(isSupportedOs, buildNumber, backgroundMode, hasCacheAccess, featureConfigured);

        return new LockScreenDiagnostics(
            OsVersion: osVersion,
            BuildNumber: buildNumber,
            IsSupportedWindowsVersion: isSupportedOs,
            BackgroundMode: backgroundMode,
            HasCacheDirectory: hasCacheDir,
            HasCacheAccess: hasCacheAccess,
            UserSid: userSid,
            FeatureOverrideConfigured: featureConfigured,
            NativeVideoApiAvailable: nativeVideoApiAvailable,
            Summary: summary
        );
    }

    private static string? GetCurrentUserSid()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static LockScreenBackgroundMode DetectBackgroundMode()
    {
        try
        {
            using var deliveryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
            if (deliveryKey?.GetValue("RotatingLockScreenEnabled") is int spotlight && spotlight == 1)
            {
                return LockScreenBackgroundMode.Spotlight;
            }

            using var lockKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lock Screen");
            if (lockKey?.GetValue("SlideshowEnabled") is int slideshow && slideshow == 1)
            {
                return LockScreenBackgroundMode.Slideshow;
            }

            return LockScreenBackgroundMode.Picture;
        }
        catch
        {
            return LockScreenBackgroundMode.Unknown;
        }
    }

    private static (bool Exists, bool Accessible) CheckCacheDirectory(string? userSid)
    {
        if (string.IsNullOrWhiteSpace(userSid))
        {
            return (false, false);
        }

        try
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft",
                "Windows",
                "SystemData",
                userSid,
                "ReadOnly"
            );

            if (!Directory.Exists(cachePath))
            {
                return (false, false);
            }

            // Test read access
            _ = Directory.GetFileSystemEntries(cachePath);
            return (true, true);
        }
        catch (UnauthorizedAccessException)
        {
            return (true, false);
        }
        catch
        {
            return (false, false);
        }
    }

    private static bool CheckFeatureOverride()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(DefaultFeatureOverrideKey);
            if (key?.GetValue("EnabledState") is int state && state == 1)
            {
                return true;
            }
        }
        catch
        {
            // Access denied or not found
        }

        return false;
    }

    private static string BuildSummaryMessage(
        bool isSupportedOs,
        int buildNumber,
        LockScreenBackgroundMode backgroundMode,
        bool hasCacheAccess,
        bool featureConfigured
    )
    {
        if (!isSupportedOs)
        {
            return "Current operating system is not supported. Moonlit requires Windows 10 (1809+) or Windows 11.";
        }

        if (backgroundMode != LockScreenBackgroundMode.Picture)
        {
            return $"Windows Lock Screen is set to '{backgroundMode}'. Personalization settings must be set to 'Picture' mode.";
        }

        if (!hasCacheAccess)
        {
            return "Windows Lock Screen cache directory is restricted by system security (Outcome B). Administrator permissions are required to modify lock-screen assets.";
        }

        if (buildNumber >= Windows11FeatureChangeBuild && !featureConfigured)
        {
            return "Windows 11 build 26200+ requires Windows Feature Management Override (ID 38943831) to allow dynamic lock-screen animation.";
        }

        return "Environment inspection complete. Note: Windows OS does not expose a native API for direct video streaming on the lock screen.";
    }
}
