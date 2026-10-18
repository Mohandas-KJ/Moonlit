using System.IO;

namespace Moonlit.Services;

/// <summary>
/// Result of a video file validation check.
/// </summary>
public sealed record VideoValidationResult(
    bool IsValid,
    string? FilePath,
    string? FileName,
    long FileSizeBytes,
    string? ErrorMessage = null
)
{
    public static VideoValidationResult Success(string filePath, long fileSizeBytes) =>
        new(true, filePath, Path.GetFileName(filePath), fileSizeBytes);

    public static VideoValidationResult Failure(string? filePath, string errorMessage) =>
        new(false, filePath, filePath != null ? Path.GetFileName(filePath) : null, 0, errorMessage);
}

/// <summary>
/// Service responsible for validating video files, checking format compatibility, and path accessibility.
/// </summary>
public sealed class VideoService
{
    public static readonly string[] SupportedExtensions =
    [
        ".mp4",
        ".m4v",
        ".mov",
        ".wmv",
        ".avi",
        ".mkv",
        ".webm"
    ];

    public static string FileDialogFilter =>
        "Video Files (*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv;*.webm)|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv;*.webm|All Files (*.*)|*.*";

    /// <summary>
    /// Validates whether a file path exists, is accessible for reading, and matches a supported video format.
    /// </summary>
    public VideoValidationResult ValidateVideoFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return VideoValidationResult.Failure(null, "No video file selected.");
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return VideoValidationResult.Failure(filePath, $"The video file '{Path.GetFileName(filePath)}' does not exist at the specified path.");
            }

            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension) || !SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return VideoValidationResult.Failure(
                    filePath,
                    $"Unsupported video extension '{extension}'. Moonlit supports: {string.Join(", ", SupportedExtensions)}."
                );
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                return VideoValidationResult.Failure(filePath, "The selected video file is empty (0 bytes).");
            }

            // Test read accessibility
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length == 0)
                {
                    return VideoValidationResult.Failure(filePath, "The video stream cannot be read.");
                }
            }

            return VideoValidationResult.Success(filePath, fileInfo.Length);
        }
        catch (UnauthorizedAccessException)
        {
            return VideoValidationResult.Failure(filePath, "Access denied. Moonlit lacks permission to read this file.");
        }
        catch (IOException ex)
        {
            return VideoValidationResult.Failure(filePath, $"File I/O error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return VideoValidationResult.Failure(filePath, $"Validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats file size in bytes to a human-readable string.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
            if (counter >= suffixes.Length - 1)
                break;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
