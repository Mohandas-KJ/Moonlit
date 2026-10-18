using Moonlit.Integration;

namespace Moonlit.Services;

/// <summary>
/// Result of an integration test or prerequisite check.
/// </summary>
public sealed record IntegrationTestResult(
    bool CanProceed,
    string Title,
    string Description,
    LockScreenDiagnostics Diagnostics
);

/// <summary>
/// Service coordinating lock-screen prerequisite checks and diagnostic testing.
/// </summary>
public sealed class LockScreenService
{
    private readonly LockScreenIntegration _integration;

    public LockScreenService(LockScreenIntegration? integration = null)
    {
        _integration = integration ?? new LockScreenIntegration();
    }

    /// <summary>
    /// Evaluates whether the system meets the basic requirements for lock-screen customization.
    /// </summary>
    public LockScreenDiagnostics GetDiagnostics() => _integration.ProbeEnvironment();

    /// <summary>
    /// Executes a safe diagnostic test of the integration environment without making unsupported system modifications.
    /// </summary>
    public IntegrationTestResult RunIntegrationTest()
    {
        var diag = _integration.ProbeEnvironment();

        if (!diag.IsSupportedWindowsVersion)
        {
            return new IntegrationTestResult(
                CanProceed: false,
                Title: "Unsupported Windows Version",
                Description: $"Moonlit detected {diag.OsVersion}. Windows 10 (1809+) or Windows 11 is required.",
                Diagnostics: diag
            );
        }

        if (diag.BackgroundMode != LockScreenBackgroundMode.Picture)
        {
            return new IntegrationTestResult(
                CanProceed: false,
                Title: "Personalization Mode Needs Attention",
                Description: $"Lock screen background is currently configured as '{diag.BackgroundMode}'. Go to Windows Settings > Personalization > Lock screen and select 'Picture'.",
                Diagnostics: diag
            );
        }

        if (!diag.HasCacheAccess)
        {
            return new IntegrationTestResult(
                CanProceed: false,
                Title: "System Security Restrictions Detected (Outcome B)",
                Description: "The Windows lock-screen cache directory (C:\\ProgramData\\Microsoft\\Windows\\SystemData) is owned by SYSTEM and requires elevated UAC permissions to access. Direct video playback without privilege escalation is not supported by Windows.",
                Diagnostics: diag
            );
        }

        if (diag.BuildNumber >= LockScreenIntegration.Windows11FeatureChangeBuild && !diag.FeatureOverrideConfigured)
        {
            return new IntegrationTestResult(
                CanProceed: false,
                Title: "Windows Feature Override Required",
                Description: $"On Windows 11 build {diag.BuildNumber}, dynamic lock-screen rendering requires disabling feature ID 38943831 via registry overrides. Without this modification, Windows displays static frames.",
                Diagnostics: diag
            );
        }

        return new IntegrationTestResult(
            CanProceed: true,
            Title: "Prerequisites Verified",
            Description: "Windows environment checks passed. Note: In compliance with PLAN.md, native video container streaming requires Microsoft platform support or elevated cache transcoding.",
            Diagnostics: diag
        );
    }
}
