namespace Moonlit.Services;

/// <summary>
/// Measures and tracks startup timestamps to evaluate Moonlit's instant launch performance.
/// </summary>
public static class StartupDiagnosticsTracker
{
    public static DateTimeOffset ProcessStartTime { get; private set; } = DateTimeOffset.UtcNow;
    public static DateTimeOffset? WindowCreatedTime { get; private set; }
    public static DateTimeOffset? WindowFirstDisplayedTime { get; private set; }
    public static DateTimeOffset? VideoInitStartedTime { get; private set; }
    public static DateTimeOffset? FirstVideoFrameDisplayedTime { get; private set; }

    public static void RecordProcessStart(DateTimeOffset? time = null)
    {
        ProcessStartTime = time ?? DateTimeOffset.UtcNow;
    }

    public static void RecordWindowCreated()
    {
        WindowCreatedTime ??= DateTimeOffset.UtcNow;
    }

    public static void RecordWindowFirstDisplayed()
    {
        WindowFirstDisplayedTime ??= DateTimeOffset.UtcNow;
    }

    public static void RecordVideoInitStarted()
    {
        VideoInitStartedTime ??= DateTimeOffset.UtcNow;
    }

    public static void RecordFirstVideoFrameDisplayed()
    {
        FirstVideoFrameDisplayedTime ??= DateTimeOffset.UtcNow;
    }

    public static string GetStartupTimingsSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Startup Performance Metrics:");
        sb.AppendLine($"  • Process Started:              {ProcessStartTime:HH:mm:ss.fff}");

        if (WindowCreatedTime.HasValue)
        {
            var ms = (WindowCreatedTime.Value - ProcessStartTime).TotalMilliseconds;
            sb.AppendLine($"  • Window Created:               +{ms:F0} ms");
        }

        if (WindowFirstDisplayedTime.HasValue)
        {
            var ms = (WindowFirstDisplayedTime.Value - ProcessStartTime).TotalMilliseconds;
            sb.AppendLine($"  • Window First Displayed:       +{ms:F0} ms (Visible to user)");
        }

        if (VideoInitStartedTime.HasValue)
        {
            var ms = (VideoInitStartedTime.Value - ProcessStartTime).TotalMilliseconds;
            sb.AppendLine($"  • Video Initialization Started: +{ms:F0} ms");
        }

        if (FirstVideoFrameDisplayedTime.HasValue)
        {
            var ms = (FirstVideoFrameDisplayedTime.Value - ProcessStartTime).TotalMilliseconds;
            sb.AppendLine($"  • First Video Frame Rendered:   +{ms:F0} ms (Seamless playback)");
        }

        return sb.ToString();
    }
}
