using Moonlit.Services;

namespace Moonlit.Tests;

public class StartupDiagnosticsTrackerTests
{
    [Fact]
    public void StartupDiagnosticsTracker_RecordsTimestampsAndGeneratesSummary()
    {
        var customStart = DateTimeOffset.UtcNow.AddSeconds(-2);
        StartupDiagnosticsTracker.RecordProcessStart(customStart);
        StartupDiagnosticsTracker.RecordWindowCreated();
        StartupDiagnosticsTracker.RecordWindowFirstDisplayed();
        StartupDiagnosticsTracker.RecordVideoInitStarted();
        StartupDiagnosticsTracker.RecordFirstVideoFrameDisplayed();

        Assert.Equal(customStart, StartupDiagnosticsTracker.ProcessStartTime);
        Assert.NotNull(StartupDiagnosticsTracker.WindowCreatedTime);
        Assert.NotNull(StartupDiagnosticsTracker.WindowFirstDisplayedTime);
        Assert.NotNull(StartupDiagnosticsTracker.VideoInitStartedTime);
        Assert.NotNull(StartupDiagnosticsTracker.FirstVideoFrameDisplayedTime);

        var summary = StartupDiagnosticsTracker.GetStartupTimingsSummary();
        Assert.Contains("Startup Performance Metrics:", summary);
        Assert.Contains("Process Started:", summary);
        Assert.Contains("Window Created:", summary);
        Assert.Contains("Window First Displayed:", summary);
        Assert.Contains("Video Initialization Started:", summary);
        Assert.Contains("First Video Frame Rendered:", summary);
    }
}
