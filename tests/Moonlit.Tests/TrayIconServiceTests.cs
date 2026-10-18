using Moonlit.Services;

namespace Moonlit.Tests;

public class TrayIconServiceTests
{
    [Fact]
    public void TrayIconService_CanBeInstantiatedAndDisposedSafely()
    {
        using var trayService = new TrayIconService(
            onOpenDashboard: () => { },
            onLockNow: () => { },
            onExit: () => { }
        );

        Assert.NotNull(trayService);
        Assert.False(trayService.IsAdded);

        // Safe disposal test
        trayService.Dispose();
        Assert.False(trayService.IsAdded);
    }
}
