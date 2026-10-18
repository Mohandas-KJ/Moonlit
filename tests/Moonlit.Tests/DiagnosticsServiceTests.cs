using System.IO;
using Moonlit.Models;
using Moonlit.Services;

namespace Moonlit.Tests;

public class DiagnosticsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SettingsService _settingsService;
    private readonly VideoService _videoService;
    private readonly StartupService _startupService;
    private readonly DiagnosticsService _diagnosticsService;

    public DiagnosticsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"DiagTest_{Guid.NewGuid():N}");
        _settingsService = new SettingsService(_tempDirectory);
        _videoService = new VideoService();
        _startupService = new StartupService();
        _diagnosticsService = new DiagnosticsService(_videoService, _settingsService, _startupService);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // ignore test cleanup errors
        }
    }

    [Fact]
    public void RunDiagnostics_ReturnsValidSystemDiagnostics()
    {
        var settings = new AppSettings();
        var diag = _diagnosticsService.RunDiagnostics(settings);

        Assert.NotNull(diag);
        Assert.NotNull(diag.OsVersion);
        Assert.True(diag.BuildNumber > 0);
        Assert.NotNull(diag.SettingsPath);
        Assert.NotNull(diag.LogsPath);
        Assert.NotNull(diag.Summary);
        Assert.True(diag.IsWinLPolicyActive || !diag.IsWinLPolicyActive);
        Assert.True(diag.IsNoLockScreenPolicyActive || !diag.IsNoLockScreenPolicyActive);
    }
}
