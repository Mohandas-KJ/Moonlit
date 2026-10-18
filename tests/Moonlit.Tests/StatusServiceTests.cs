using System.IO;
using Moonlit.Models;
using Moonlit.Services;

namespace Moonlit.Tests;

public class StatusServiceTests
{
    private readonly VideoService _videoService = new();
    private readonly StatusService _statusService;

    public StatusServiceTests()
    {
        _statusService = new StatusService(_videoService);
    }

    [Fact]
    public void EvaluateStatus_WhenNoVideoPath_ReturnsNotReady()
    {
        var settings = new AppSettings { VideoPath = null };

        var status = _statusService.EvaluateStatus(settings, startupVerified: true);

        Assert.Equal(StatusLevel.NotReady, status.Level);
        Assert.Contains("Choose a video", status.Header);
        Assert.NotNull(status.CorrectiveAction);
    }

    [Fact]
    public void EvaluateStatus_WhenVideoDoesNotExist_ReturnsNotReady()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.mp4");
        var settings = new AppSettings { VideoPath = missingPath };

        var status = _statusService.EvaluateStatus(settings, startupVerified: true);

        Assert.Equal(StatusLevel.NotReady, status.Level);
        Assert.Contains("unavailable or invalid", status.Header);
        Assert.NotNull(status.CorrectiveAction);
    }

    [Fact]
    public void EvaluateStatus_WhenLockExperienceDisabled_ReturnsAttention()
    {
        var tempVideo = Path.Combine(Path.GetTempPath(), $"test_status_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(tempVideo, [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);

        try
        {
            var settings = new AppSettings
            {
                VideoPath = tempVideo,
                LockExperienceEnabled = false
            };

            var status = _statusService.EvaluateStatus(settings, startupVerified: true);

            Assert.Equal(StatusLevel.Attention, status.Level);
            Assert.Contains("Lock screen experience is disabled", status.Header);
        }
        finally
        {
            File.Delete(tempVideo);
        }
    }

    [Fact]
    public void EvaluateStatus_WhenStartupUnconfirmed_ReturnsAttention()
    {
        var tempVideo = Path.Combine(Path.GetTempPath(), $"test_status_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(tempVideo, [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);

        try
        {
            var settings = new AppSettings
            {
                VideoPath = tempVideo,
                LockExperienceEnabled = true,
                LaunchAtSignIn = true
            };

            var status = _statusService.EvaluateStatus(settings, startupVerified: false);

            Assert.Equal(StatusLevel.Attention, status.Level);
            Assert.Contains("Startup preference not confirmed", status.Header);
        }
        finally
        {
            File.Delete(tempVideo);
        }
    }

    [Fact]
    public void EvaluateStatus_WhenAllChecksPass_ReturnsReady()
    {
        var tempVideo = Path.Combine(Path.GetTempPath(), $"test_status_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(tempVideo, [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);

        try
        {
            var settings = new AppSettings
            {
                VideoPath = tempVideo,
                LockExperienceEnabled = true,
                LaunchAtSignIn = false
            };

            var status = _statusService.EvaluateStatus(settings, startupVerified: true);

            Assert.Equal(StatusLevel.Ready, status.Level);
            Assert.Contains("Ready", status.Header);
        }
        finally
        {
            File.Delete(tempVideo);
        }
    }
}
