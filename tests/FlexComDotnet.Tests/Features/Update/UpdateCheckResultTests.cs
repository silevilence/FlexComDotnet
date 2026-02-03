using FlexComDotnet.Core.Features.Update.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// UpdateCheckResult 模型测试
/// </summary>
public class UpdateCheckResultTests
{
    #region NoUpdate Tests

    [Fact]
    public void NoUpdate_ShouldCreateCorrectResult()
    {
        // Arrange
        var currentVersion = VersionInfo.Parse("1.0.0");

        // Act
        var result = UpdateCheckResult.NoUpdate(currentVersion);

        // Assert
        result.HasUpdate.Should().BeFalse();
        result.IsSuccess.Should().BeTrue();
        result.CurrentVersion.Should().Be(currentVersion);
        result.LatestVersion.Should().BeNull();
        result.ReleaseInfo.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region Available Tests

    [Fact]
    public void Available_ShouldCreateCorrectResult()
    {
        // Arrange
        var currentVersion = VersionInfo.Parse("1.0.0");
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v2.0.0",
            Name = "Release 2.0.0",
            Body = "New features",
            PublishedAt = DateTime.Now,
            Assets =
            [
                new ReleaseAsset
                {
                    Name = "app.zip",
                    DownloadUrl = "https://example.com/app.zip",
                    Size = 1024 * 1024
                }
            ]
        };

        // Act
        var result = UpdateCheckResult.Available(currentVersion, releaseInfo);

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.CurrentVersion.Should().Be(currentVersion);
        result.LatestVersion.Should().NotBeNull();
        result.LatestVersion!.Major.Should().Be(2);
        result.ReleaseInfo.Should().Be(releaseInfo);
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region Failed Tests

    [Fact]
    public void Failed_ShouldCreateCorrectResult()
    {
        // Arrange
        var currentVersion = VersionInfo.Parse("1.0.0");
        var errorMessage = "Network error";

        // Act
        var result = UpdateCheckResult.Failed(currentVersion, errorMessage);

        // Assert
        result.HasUpdate.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.CurrentVersion.Should().Be(currentVersion);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    #endregion
}
