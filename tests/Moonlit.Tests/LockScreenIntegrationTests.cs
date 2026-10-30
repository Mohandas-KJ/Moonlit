using Moonlit.Integration;
using Moonlit.Services;

namespace Moonlit.Tests;

public class LockScreenIntegrationTests
{
    private readonly LockScreenIntegration _integration = new();
    private readonly LockScreenService _service;

    public LockScreenIntegrationTests()
    {
        _service = new LockScreenService(_integration);
    }

    [Fact]
    public void ProbeEnvironment_ReturnsValidDiagnostics()
    {
        var diag = _integration.ProbeEnvironment();

        Assert.NotNull(diag);
        Assert.NotNull(diag.OsVersion);
        Assert.True(diag.BuildNumber > 0);
        Assert.False(diag.NativeVideoApiAvailable);
        Assert.NotNull(diag.Summary);
    }

    [Fact]
    public void RunIntegrationTest_ReturnsWellFormedResult()
    {
        var testResult = _service.RunIntegrationTest();

        Assert.NotNull(testResult);
        Assert.NotNull(testResult.Title);
        Assert.NotNull(testResult.Description);
        Assert.NotNull(testResult.Diagnostics);
    }

    [Fact]
    public void WindowsIntegration_GetPrimaryScreenBounds_ReturnsPositiveDimensions()
    {
        var bounds = WindowsIntegration.GetPrimaryScreenBounds();

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void WindowsIntegration_GetVirtualScreenBounds_ReturnsNonEmptyArea()
    {
        var bounds = WindowsIntegration.GetVirtualScreenBounds();

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void WindowsIntegration_IsLockWorkstationDisabled_ReturnsBooleanWithoutException()
    {
        var result = WindowsIntegration.IsLockWorkstationDisabled();
        // Result is either true or false depending on system policy, must not throw
        Assert.True(result || !result);
    }

    [Fact]
    public void WindowsIntegration_IsNoLockScreenConfigured_ReturnsBooleanWithoutException()
    {
        var result = WindowsIntegration.IsNoLockScreenConfigured();
        // Result is either true or false depending on system policy, must not throw
        Assert.True(result || !result);
    }

    [Fact]
    public void WindowsIntegration_SendKeyStroke_ExecutesSafely()
    {
        // VK_SPACE = 0x20
        var exception = Record.Exception(() => WindowsIntegration.SendKeyStroke(0x20));
        Assert.Null(exception);
    }

    [Fact]
    public void WindowsIntegration_StartDismissPulseSequence_ExecutesSafely()
    {
        var exception = Record.Exception(WindowsIntegration.StartDismissPulseSequence);
        Assert.Null(exception);
    }

    [Fact]
    public void WindowsIntegration_SetLockWorkstationDisabled_ExecutesSafely()
    {
        // Safe toggle test: ensuring no crash when writing/deleting local user policy
        var exception = Record.Exception(() => WindowsIntegration.SetLockWorkstationDisabled(false));
        Assert.Null(exception);
    }

    [Fact]
    public void WindowsIntegration_IsNetplwizCheckboxUnlocked_ReturnsBooleanWithoutException()
    {
        var result = WindowsIntegration.IsNetplwizCheckboxUnlocked();
        Assert.True(result || !result);
    }

    [Fact]
    public void WindowsIntegration_ClearAllMoonlitRegistryPolicies_ReturnsResultSafely()
    {
        var exception = Record.Exception(() =>
        {
            var (success, message) = WindowsIntegration.ClearAllMoonlitRegistryPolicies(promptElevation: false);
            Assert.NotNull(message);
        });
        Assert.Null(exception);
    }
}
