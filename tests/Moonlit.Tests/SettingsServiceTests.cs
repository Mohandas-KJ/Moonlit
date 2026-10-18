using System.IO;
using Moonlit.Models;
using Moonlit.Services;

namespace Moonlit.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SettingsService _settingsService;

    public SettingsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"MoonlitTest_{Guid.NewGuid():N}");
        _settingsService = new SettingsService(_tempDirectory);
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
            // Ignore test cleanup exceptions
        }
    }

    [Fact]
    public void LoadSettings_WhenFileDoesNotExist_ReturnsDefaultsAndCreatesFile()
    {
        var settings = _settingsService.LoadSettings();

        Assert.NotNull(settings);
        Assert.Null(settings.VideoPath);
        Assert.True(settings.LoopPlayback);
        Assert.False(settings.LaunchAtSignIn);
        Assert.True(File.Exists(_settingsService.SettingsFilePath));
    }

    [Fact]
    public void SaveAndLoadSettings_PersistsAllValuesCorrectly()
    {
        var settingsToSave = new AppSettings
        {
            VideoPath = @"C:\Videos\Atmosphere.mp4",
            LoopPlayback = false,
            LaunchAtSignIn = true
        };

        var saveSuccess = _settingsService.SaveSettings(settingsToSave);
        Assert.True(saveSuccess);

        var loadedSettings = _settingsService.LoadSettings();
        Assert.NotNull(loadedSettings);
        Assert.Equal(@"C:\Videos\Atmosphere.mp4", loadedSettings.VideoPath);
        Assert.False(loadedSettings.LoopPlayback);
        Assert.True(loadedSettings.LaunchAtSignIn);
        Assert.NotNull(loadedSettings.LastSaved);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsSuccessfully()
    {
        var settingsToSave = new AppSettings
        {
            VideoPath = @"C:\Media\Aurora.mkv",
            LoopPlayback = true,
            LaunchAtSignIn = false
        };

        var saveSuccess = await _settingsService.SaveSettingsAsync(settingsToSave);
        Assert.True(saveSuccess);

        var loadedSettings = _settingsService.LoadSettings();
        Assert.Equal(@"C:\Media\Aurora.mkv", loadedSettings.VideoPath);
        Assert.True(loadedSettings.LoopPlayback);
        Assert.False(loadedSettings.LaunchAtSignIn);
    }

    [Fact]
    public void LoadSettings_MalformedJson_RevertsToDefaultsWithoutThrowing()
    {
        File.WriteAllText(_settingsService.SettingsFilePath, "{ invalid json content !!");

        var settings = _settingsService.LoadSettings();

        Assert.NotNull(settings);
        Assert.Null(settings.VideoPath);
        Assert.True(settings.LoopPlayback);
    }

    [Fact]
    public void LogMessage_WritesToLogsFile()
    {
        _settingsService.LogMessage("Test log entry");

        var logFile = Path.Combine(_settingsService.LogsDirectory, "moonlit.log");
        Assert.True(File.Exists(logFile));

        var logContent = File.ReadAllText(logFile);
        Assert.Contains("Test log entry", logContent);
    }
}
