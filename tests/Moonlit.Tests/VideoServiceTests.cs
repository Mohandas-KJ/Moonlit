using System.IO;
using Moonlit.Services;

namespace Moonlit.Tests;

public class VideoServiceTests
{
    private readonly VideoService _videoService = new();

    [Fact]
    public void ValidateVideoFile_NullOrEmptyPath_ReturnsFailure()
    {
        var result1 = _videoService.ValidateVideoFile(null);
        Assert.False(result1.IsValid);
        Assert.Contains("No video file selected", result1.ErrorMessage);

        var result2 = _videoService.ValidateVideoFile("   ");
        Assert.False(result2.IsValid);
        Assert.Contains("No video file selected", result2.ErrorMessage);
    }

    [Fact]
    public void ValidateVideoFile_NonExistentFile_ReturnsFailure()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing_video_{Guid.NewGuid():N}.mp4");
        var result = _videoService.ValidateVideoFile(nonExistentPath);

        Assert.False(result.IsValid);
        Assert.Contains("does not exist", result.ErrorMessage);
    }

    [Fact]
    public void ValidateVideoFile_UnsupportedExtension_ReturnsFailure()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_file_{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempFile, "not a video");

        try
        {
            var result = _videoService.ValidateVideoFile(tempFile);
            Assert.False(result.IsValid);
            Assert.Contains("Unsupported video extension", result.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidateVideoFile_ZeroByteFile_ReturnsFailure()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"empty_video_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(tempFile, []);

        try
        {
            var result = _videoService.ValidateVideoFile(tempFile);
            Assert.False(result.IsValid);
            Assert.Contains("empty (0 bytes)", result.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidateVideoFile_ValidFile_ReturnsSuccess()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"valid_video_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70]); // dummy mp4 header

        try
        {
            var result = _videoService.ValidateVideoFile(tempFile);
            Assert.True(result.IsValid);
            Assert.Equal(8, result.FileSizeBytes);
            Assert.Equal(Path.GetFileName(tempFile), result.FileName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(500, "500.0 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    public void FormatFileSize_FormatsCorrectly(long bytes, string expected)
    {
        var formatted = VideoService.FormatFileSize(bytes);
        Assert.Equal(expected, formatted);
    }
}
