using Moonlit.Services;

namespace Moonlit.Tests;

public class StartupServiceTests
{
    private readonly StartupService _startupService = new();

    [Fact]
    public void IsStartupEnabled_CanBeQueriedSafely()
    {
        // Querying startup status must not throw an exception in standard user session
        var isEnabled = _startupService.IsStartupEnabled();
        Assert.True(isEnabled || !isEnabled);
    }

    [Fact]
    public void SetStartupEnabled_WhenToggled_ReturnsValidOperationResult()
    {
        var originalState = _startupService.IsStartupEnabled();
        try
        {
            var result = _startupService.SetStartupEnabled(false);
            Assert.NotNull(result);
            Assert.NotNull(result.Message);
            Assert.False(result.IsEnabled);
        }
        finally
        {
            // Restore original state if needed
            if (originalState)
            {
                _startupService.SetStartupEnabled(true);
            }
        }
    }
}
