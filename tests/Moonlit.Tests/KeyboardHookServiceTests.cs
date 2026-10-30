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

    [Fact]
    public void KeyboardHookService_LastShortcutWasWinL_DefaultsToFalse()
    {
        using var hookService = new KeyboardHookService();
        Assert.False(hookService.LastShortcutWasWinL);
    }

    [Fact]
    public void KeyboardHookService_IsKioskLockActive_CanBeToggled()
    {
        using var hookService = new KeyboardHookService();
        Assert.False(hookService.IsKioskLockActive);

        hookService.IsKioskLockActive = true;
        Assert.True(hookService.IsKioskLockActive);

        hookService.IsKioskLockActive = false;
        Assert.False(hookService.IsKioskLockActive);
    }
}
