using System.IO;
using System.Text.Json;
using Moonlit.Models;

namespace Moonlit.Services;

/// <summary>
/// Manages loading, saving, and defaults for application settings stored in JSON format under %LOCALAPPDATA%\Moonlit.
/// </summary>
public sealed class SettingsService
{
    private readonly string _settingsFilePath;
    private readonly string _appDataDirectory;
    private readonly string _logsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Lock _lock = new();

    public SettingsService(string? customBasePath = null)
    {
        var baseDir = customBasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Moonlit"
        );

        _appDataDirectory = baseDir;
        _logsDirectory = Path.Combine(baseDir, "logs");
        _settingsFilePath = Path.Combine(baseDir, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        EnsureDirectoriesExist();
    }

    public string AppDataDirectory => _appDataDirectory;
    public string LogsDirectory => _logsDirectory;
    public string SettingsFilePath => _settingsFilePath;

    private void EnsureDirectoriesExist()
    {
        try
        {
            if (!Directory.Exists(_appDataDirectory))
            {
                Directory.CreateDirectory(_appDataDirectory);
            }
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to ensure app data directories: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the stored settings, or returns defaults if the file does not exist or is malformed.
    /// </summary>
    public AppSettings LoadSettings()
    {
        lock (_lock)
        {
            if (!File.Exists(_settingsFilePath))
            {
                var defaults = new AppSettings();
                SaveSettings(defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                LogMessage($"Warning: Could not read settings file. Reverting to default configuration. Error: {ex.Message}");
                return new AppSettings();
            }
        }
    }

    /// <summary>
    /// Persists settings to JSON asynchronously.
    /// </summary>
    public async Task<bool> SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            settings.LastSaved = DateTimeOffset.UtcNow;
            var json = JsonSerializer.Serialize(settings, _jsonOptions);

            EnsureDirectoriesExist();
            var tempPath = _settingsFilePath + ".tmp";

            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);

            LogMessage("Settings successfully saved.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Error saving settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Persists settings synchronously.
    /// </summary>
    public bool SaveSettings(AppSettings settings)
    {
        lock (_lock)
        {
            try
            {
                settings.LastSaved = DateTimeOffset.UtcNow;
                var json = JsonSerializer.Serialize(settings, _jsonOptions);

                EnsureDirectoriesExist();
                var tempPath = _settingsFilePath + ".tmp";

                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsFilePath, overwrite: true);

                LogMessage("Settings successfully saved.");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving settings: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Lightweight non-sensitive logger to %LOCALAPPDATA%\Moonlit\logs\moonlit.log.
    /// </summary>
    public void LogMessage(string message)
    {
        try
        {
            var logFile = Path.Combine(_logsDirectory, "moonlit.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, line);
        }
        catch
        {
            // Lightweight logging must never crash the application.
        }
    }
}
