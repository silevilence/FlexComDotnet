using FlexComDotnet.Core.Features.Update.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// ReleaseInfo 模型测试
/// </summary>
public class ReleaseInfoTests
{
    [Fact]
    public void Version_ShouldParseFromTagName()
    {
        // Arrange
        var release = new ReleaseInfo
        {
            TagName = "v1.2.3",
            Name = "Release 1.2.3"
        };

        // Act
        var version = release.Version;

        // Assert
        version.Major.Should().Be(1);
        version.Minor.Should().Be(2);
        version.Patch.Should().Be(3);
    }

    [Fact]
    public void Version_WithPrereleaseTag_ShouldParseCorrectly()
    {
        // Arrange
        var release = new ReleaseInfo
        {
            TagName = "v2.0.0-beta.1",
            Name = "Release 2.0.0 Beta 1"
        };

        // Act
        var version = release.Version;

        // Assert
        version.Major.Should().Be(2);
        version.Minor.Should().Be(0);
        version.Patch.Should().Be(0);
        version.Prerelease.Should().Be("beta.1");
    }

    [Fact]
    public void Assets_DefaultValue_ShouldBeEmpty()
    {
        // Arrange
        var release = new ReleaseInfo();

        // Assert
        release.Assets.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseAsset_ShouldStoreAllProperties()
    {
        // Arrange
        var asset = new ReleaseAsset
        {
            Name = "FlexComDotnet_1.0.0.zip",
            DownloadUrl = "https://github.com/silevilence/FlexComDotnet/releases/download/v1.0.0/FlexComDotnet_1.0.0.zip",
            Size = 15 * 1024 * 1024,
            ContentType = "application/zip",
            DownloadCount = 100
        };

        // Assert
        asset.Name.Should().Be("FlexComDotnet_1.0.0.zip");
        asset.DownloadUrl.Should().Contain("github.com");
        asset.Size.Should().Be(15 * 1024 * 1024);
        asset.ContentType.Should().Be("application/zip");
        asset.DownloadCount.Should().Be(100);
    }
}
