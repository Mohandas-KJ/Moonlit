using Moonlit.Services;

namespace Moonlit.Tests;

public class KeyboardHookServiceTests
{
    [Fact]
    public void KeyboardHookService_CanBeInstantiatedAndDisposedSafely()
    {
        using var hookService = new KeyboardHookService();

        Assert.False(hookService.IsHookActive);

        hookService.StartHook();
        Assert.True(hookService.IsHookActive);

        hookService.StopHook();
        Assert.False(hookService.IsHookActive);
    }
}
