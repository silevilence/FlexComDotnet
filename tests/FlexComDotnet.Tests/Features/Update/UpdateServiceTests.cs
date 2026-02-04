using FlexComDotnet.Core.Features.Update.Models;
using FlexComDotnet.Core.Features.Update.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// UpdateService 测试
/// </summary>
public class UpdateServiceTests
{
    private readonly Mock<IVersionService> _versionServiceMock;
    private readonly Mock<IGitHubReleaseService> _releaseServiceMock;
    private readonly Mock<IDownloadService> _downloadServiceMock;
    private readonly UpdateService _sut;

    public UpdateServiceTests()
    {
        _versionServiceMock = new Mock<IVersionService>();
        _releaseServiceMock = new Mock<IGitHubReleaseService>();
        _downloadServiceMock = new Mock<IDownloadService>();

        _sut = new UpdateService(
            _versionServiceMock.Object,
            _releaseServiceMock.Object,
            _downloadServiceMock.Object);
    }

    #region CheckForUpdateAsync Tests

    [Fact]
    public async Task CheckForUpdateAsync_WhenUpdateAvailable_ShouldReturnAvailableResult()
    {
        // Arrange
        var currentVersion = VersionInfo.Parse("1.0.0");
        var latestRelease = new ReleaseInfo
        {
            TagName = "v2.0.0",
            Name = "Release 2.0.0",
            Body = "New features"
        };

        _versionServiceMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);
        _releaseServiceMock.Setup(x => x.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestRelease);
        _versionServiceMock.Setup(x => x.IsUpdateAvailable(It.IsAny<VersionInfo>()))
            .Returns(true);

        // Act
        var result = await _sut.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeTrue();
        result.CurrentVersion.Should().Be(currentVersion);
        result.LatestVersion!.Major.Should().Be(2);
        result.ReleaseInfo.Should().Be(latestRelease);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNoUpdate_ShouldReturnNoUpdateResult()
    {
        // Arrange
        var currentVersion = VersionInfo.Parse("2.0.0");
        var latestRelease = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Name = "Release 1.0.0"
        };

        _versionServiceMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);
        _releaseServiceMock.Setup(x => x.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestRelease);
        _versionServiceMock.Setup(x => x.IsUpdateAvailable(It.IsAny<VersionInfo>()))
            .Returns(false);

        // Act
        var result = await _sut.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenReleaseServiceFails_ShouldReturnFailedResult()
    {
        // Arrange
        var currentVersion = VersionInfo.Parse("1.0.0");

        _versionServiceMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);
        _releaseServiceMock.Setup(x => x.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReleaseInfo?)null);

        // Act
        var result = await _sut.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.HasUpdate.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckForUpdateAsync_ShouldRaiseCheckingForUpdateEvent()
    {
        // Arrange
        var eventRaised = false;
        _sut.CheckingForUpdate += (_, checking) => eventRaised = true;

        _versionServiceMock.Setup(x => x.GetCurrentVersion()).Returns(VersionInfo.Parse("1.0.0"));
        _releaseServiceMock.Setup(x => x.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReleaseInfo { TagName = "v1.0.0" });

        // Act
        await _sut.CheckForUpdateAsync();

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion

    #region DownloadUpdateAsync Tests

    [Fact]
    public async Task DownloadUpdateAsync_WithValidAsset_ShouldDownloadFile()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Assets =
            [
                new ReleaseAsset
                {
                    Name = "FlexComDotnet_1.0.0.zip",
                    DownloadUrl = "https://example.com/download.zip",
                    Size = 1024
                }
            ]
        };

        var downloadPath = Path.Combine(Path.GetTempPath(), "FlexComDotnet_1.0.0.zip");

        _versionServiceMock.Setup(x => x.GetInstallationType())
            .Returns(InstallationType.Portable);
        _downloadServiceMock.Setup(x => x.GetDownloadDirectory())
            .Returns(Path.GetTempPath());
        _downloadServiceMock.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DownloadUpdateAsync(releaseInfo);

        // Assert
        result.Should().NotBeNull();
        _downloadServiceMock.Verify(x => x.DownloadFileAsync(
            releaseInfo.Assets[0].DownloadUrl,
            It.IsAny<string>(),
            It.IsAny<Action<DownloadProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithNoAssets_ShouldReturnNull()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Assets = []
        };

        _versionServiceMock.Setup(x => x.GetInstallationType())
            .Returns(InstallationType.Portable);

        // Act
        var result = await _sut.DownloadUpdateAsync(releaseInfo);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenDownloadFails_ShouldReturnNull()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Assets =
            [
                new ReleaseAsset
                {
                    Name = "app.zip",
                    DownloadUrl = "https://example.com/download.zip"
                }
            ]
        };

        _versionServiceMock.Setup(x => x.GetInstallationType())
            .Returns(InstallationType.Portable);
        _downloadServiceMock.Setup(x => x.GetDownloadDirectory())
            .Returns(Path.GetTempPath());
        _downloadServiceMock.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DownloadUpdateAsync(releaseInfo);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenPortable_ShouldPreferZipOverMsix()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Assets =
            [
                new ReleaseAsset
                {
                    Name = "FlexComDotnet_1.0.0.msix",
                    DownloadUrl = "https://example.com/app.msix"
                },
                new ReleaseAsset
                {
                    Name = "FlexComDotnet_1.0.0.zip",
                    DownloadUrl = "https://example.com/app.zip"
                }
            ]
        };

        _versionServiceMock.Setup(x => x.GetInstallationType())
            .Returns(InstallationType.Portable);
        _downloadServiceMock.Setup(x => x.GetDownloadDirectory())
            .Returns(Path.GetTempPath());
        _downloadServiceMock.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.DownloadUpdateAsync(releaseInfo);

        // Assert
        _downloadServiceMock.Verify(x => x.DownloadFileAsync(
            "https://example.com/app.zip",
            It.IsAny<string>(),
            It.IsAny<Action<DownloadProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenMsix_ShouldPreferMsixOverZip()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Assets =
            [
                new ReleaseAsset
                {
                    Name = "FlexComDotnet_1.0.0.zip",
                    DownloadUrl = "https://example.com/app.zip"
                },
                new ReleaseAsset
                {
                    Name = "FlexComDotnet_1.0.0.msix",
                    DownloadUrl = "https://example.com/app.msix"
                }
            ]
        };

        _versionServiceMock.Setup(x => x.GetInstallationType())
            .Returns(InstallationType.Msix);
        _downloadServiceMock.Setup(x => x.GetDownloadDirectory())
            .Returns(Path.GetTempPath());
        _downloadServiceMock.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.DownloadUpdateAsync(releaseInfo);

        // Assert
        _downloadServiceMock.Verify(x => x.DownloadFileAsync(
            "https://example.com/app.msix",
            It.IsAny<string>(),
            It.IsAny<Action<DownloadProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenMsixAndOnlyZipAvailable_ShouldFallbackToZip()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v1.0.0",
            Assets =
            [
                new ReleaseAsset
                {
                    Name = "FlexComDotnet_1.0.0.zip",
                    DownloadUrl = "https://example.com/app.zip"
                }
            ]
        };

        _versionServiceMock.Setup(x => x.GetInstallationType())
            .Returns(InstallationType.Msix);
        _downloadServiceMock.Setup(x => x.GetDownloadDirectory())
            .Returns(Path.GetTempPath());
        _downloadServiceMock.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.DownloadUpdateAsync(releaseInfo);

        // Assert
        _downloadServiceMock.Verify(x => x.DownloadFileAsync(
            "https://example.com/app.zip",
            It.IsAny<string>(),
            It.IsAny<Action<DownloadProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CurrentVersion Tests

    [Fact]
    public void CurrentVersion_ShouldReturnVersionFromVersionService()
    {
        // Arrange
        var expectedVersion = VersionInfo.Parse("1.2.3");
        _versionServiceMock.Setup(x => x.GetCurrentVersion()).Returns(expectedVersion);

        // Act
        var result = _sut.CurrentVersion;

        // Assert
        result.Should().Be(expectedVersion);
    }

    #endregion
}
