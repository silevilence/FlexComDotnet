using FlexComDotnet.Core.Features.Update.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// DownloadProgress 模型测试
/// </summary>
public class DownloadProgressTests
{
    #region ProgressPercentage Tests

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(100, 100, 100)]
    [InlineData(25, 200, 12.5)]
    [InlineData(1024 * 1024, 10 * 1024 * 1024, 10)]
    public void ProgressPercentage_WithKnownTotalBytes_ShouldCalculateCorrectly(
        long bytesReceived, long totalBytes, double expectedPercentage)
    {
        // Arrange
        var progress = new DownloadProgress
        {
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        };

        // Act & Assert
        progress.ProgressPercentage.Should().Be(expectedPercentage);
    }

    [Fact]
    public void ProgressPercentage_WithUnknownTotalBytes_ShouldReturnZero()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            BytesReceived = 1000,
            TotalBytes = null
        };

        // Act & Assert
        progress.ProgressPercentage.Should().Be(0);
    }

    [Fact]
    public void ProgressPercentage_WithZeroTotalBytes_ShouldReturnZero()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            BytesReceived = 0,
            TotalBytes = 0
        };

        // Act & Assert
        progress.ProgressPercentage.Should().Be(0);
    }

    #endregion

    #region EstimatedTimeRemaining Tests

    [Fact]
    public void EstimatedTimeRemaining_WithValidData_ShouldCalculateCorrectly()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            BytesReceived = 5 * 1024 * 1024,  // 5 MB
            TotalBytes = 10 * 1024 * 1024,    // 10 MB
            BytesPerSecond = 1024 * 1024      // 1 MB/s
        };

        // Act
        var remaining = progress.EstimatedTimeRemaining;

        // Assert
        remaining.Should().NotBeNull();
        remaining!.Value.TotalSeconds.Should().BeApproximately(5, 0.1);
    }

    [Fact]
    public void EstimatedTimeRemaining_WithUnknownTotalBytes_ShouldReturnNull()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            BytesReceived = 1000,
            TotalBytes = null,
            BytesPerSecond = 100
        };

        // Act & Assert
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    [Fact]
    public void EstimatedTimeRemaining_WithZeroSpeed_ShouldReturnNull()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            BytesReceived = 1000,
            TotalBytes = 10000,
            BytesPerSecond = 0
        };

        // Act & Assert
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    #endregion

    #region FormattedBytes Tests

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(1024 * 1024 * 1024, "1 GB")]
    [InlineData(1536 * 1024 * 1024, "1.5 GB")]
    public void FormattedBytesReceived_ShouldFormatCorrectly(long bytes, string expected)
    {
        // Arrange
        var progress = new DownloadProgress { BytesReceived = bytes };

        // Act & Assert
        progress.FormattedBytesReceived.Should().Be(expected);
    }

    [Fact]
    public void FormattedTotalBytes_WithKnownSize_ShouldFormat()
    {
        // Arrange
        var progress = new DownloadProgress { TotalBytes = 1024 * 1024 };

        // Act & Assert
        progress.FormattedTotalBytes.Should().Be("1 MB");
    }

    [Fact]
    public void FormattedTotalBytes_WithUnknownSize_ShouldReturnNull()
    {
        // Arrange
        var progress = new DownloadProgress { TotalBytes = null };

        // Act & Assert
        progress.FormattedTotalBytes.Should().BeNull();
    }

    [Theory]
    [InlineData(1024, "1 KB/s")]
    [InlineData(1024 * 1024, "1 MB/s")]
    public void FormattedSpeed_ShouldFormatCorrectly(long bytesPerSecond, string expected)
    {
        // Arrange
        var progress = new DownloadProgress { BytesPerSecond = bytesPerSecond };

        // Act & Assert
        progress.FormattedSpeed.Should().Be(expected);
    }

    #endregion
}
