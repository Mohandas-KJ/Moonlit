using System.Text.Json;
using Moonlit.Models;

namespace Moonlit.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Null(settings.VideoPath);
        Assert.True(settings.LoopPlayback);
        Assert.False(settings.LaunchAtSignIn);
        Assert.True(settings.LockExperienceEnabled);
        Assert.True(settings.InterceptWinL);
        Assert.True(settings.JumpToPasswordPrompt);
        Assert.True(settings.StartOnBootAsLockScreen);
        Assert.False(settings.Use24HourFormat);
        Assert.False(settings.ShowSeconds);
        Assert.Equal("Center", settings.ClockAlignment);
        Assert.Equal(0.25, settings.DimmingOpacity);
    }

    [Fact]
    public void AppSettings_Clone_CreatesExactIndependentCopy()
    {
        var original = new AppSettings
        {
            VideoPath = @"C:\Media\Cinematic.mp4",
            LoopPlayback = false,
            LaunchAtSignIn = true,
            LockExperienceEnabled = true,
            InterceptWinL = true,
            JumpToPasswordPrompt = true,
            StartOnBootAsLockScreen = true,
            Use24HourFormat = true,
            ShowSeconds = true,
            ClockAlignment = "BottomLeft",
            DimmingOpacity = 0.40,
            LastSaved = DateTimeOffset.UtcNow
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.VideoPath, clone.VideoPath);
        Assert.Equal(original.LoopPlayback, clone.LoopPlayback);
        Assert.Equal(original.LaunchAtSignIn, clone.LaunchAtSignIn);
        Assert.Equal(original.LockExperienceEnabled, clone.LockExperienceEnabled);
        Assert.Equal(original.InterceptWinL, clone.InterceptWinL);
        Assert.Equal(original.JumpToPasswordPrompt, clone.JumpToPasswordPrompt);
        Assert.Equal(original.StartOnBootAsLockScreen, clone.StartOnBootAsLockScreen);
        Assert.Equal(original.Use24HourFormat, clone.Use24HourFormat);
        Assert.Equal(original.ShowSeconds, clone.ShowSeconds);
        Assert.Equal(original.ClockAlignment, clone.ClockAlignment);
        Assert.Equal(original.DimmingOpacity, clone.DimmingOpacity);
        Assert.Equal(original.LastSaved, clone.LastSaved);
    }

    [Fact]
    public void AppSettings_SerializationAndDeserialization_PreservesAllProperties()
    {
        var original = new AppSettings
        {
            VideoPath = @"C:\Media\NightSky.mp4",
            LoopPlayback = true,
            LaunchAtSignIn = false,
            LockExperienceEnabled = true,
            InterceptWinL = false,
            JumpToPasswordPrompt = true,
            StartOnBootAsLockScreen = true,
            Use24HourFormat = true,
            ShowSeconds = false,
            ClockAlignment = "TopLeft",
            DimmingOpacity = 0.35
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.VideoPath, deserialized.VideoPath);
        Assert.Equal(original.ClockAlignment, deserialized.ClockAlignment);
        Assert.Equal(original.DimmingOpacity, deserialized.DimmingOpacity);
        Assert.Equal(original.Use24HourFormat, deserialized.Use24HourFormat);
        Assert.False(deserialized.InterceptWinL);
        Assert.True(deserialized.JumpToPasswordPrompt);
        Assert.True(deserialized.StartOnBootAsLockScreen);
    }
}
