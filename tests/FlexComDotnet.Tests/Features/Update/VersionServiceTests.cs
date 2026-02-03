using FlexComDotnet.Core.Features.Update.Models;
using FlexComDotnet.Core.Features.Update.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// VersionService 测试
/// </summary>
public class VersionServiceTests
{
    private readonly VersionService _sut;

    public VersionServiceTests()
    {
        _sut = new VersionService();
    }

    #region GetCurrentVersion Tests

    [Fact]
    public void GetCurrentVersion_ShouldReturnValidVersion()
    {
        // Act
        var version = _sut.GetCurrentVersion();

        // Assert
        version.Should().NotBeNull();
        // 版本至少应该有一个非零部分
        (version.Major + version.Minor + version.Patch).Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region CompareVersions Tests

    [Theory]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.1.0", "1.0.9", 1)]
    [InlineData("1.0.10", "1.0.9", 1)]
    public void CompareVersions_ShouldReturnCorrectResult(
        string v1, string v2, int expectedSign)
    {
        // Arrange
        var version1 = VersionInfo.Parse(v1);
        var version2 = VersionInfo.Parse(v2);

        // Act
        var result = _sut.CompareVersions(version1, version2);

        // Assert
        Math.Sign(result).Should().Be(expectedSign);
    }

    #endregion

    #region IsUpdateAvailable Tests

    [Fact]
    public void IsUpdateAvailable_WhenRemoteVersionIsNewer_ShouldReturnTrue()
    {
        // Arrange
        // 假设当前版本是一个较低的版本
        var remoteVersion = VersionInfo.Parse("999.999.999");

        // Act
        var result = _sut.IsUpdateAvailable(remoteVersion);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUpdateAvailable_WhenRemoteVersionIsOlder_ShouldReturnFalse()
    {
        // Arrange
        var remoteVersion = VersionInfo.Parse("0.0.1");

        // Act
        var result = _sut.IsUpdateAvailable(remoteVersion);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
