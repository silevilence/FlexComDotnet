using FlexComDotnet.Core.Features.Update.Models;
using FlexComDotnet.Core.Features.Update.Services;
using FlexComDotnet.Core.Features.Update.ViewModels;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// UpdateViewModel 测试
/// </summary>
public class UpdateViewModelTests
{
    private readonly Mock<IUpdateService> _updateServiceMock;
    private readonly UpdateViewModel _sut;

    public UpdateViewModelTests()
    {
        _updateServiceMock = new Mock<IUpdateService>();
        _updateServiceMock.Setup(x => x.CurrentVersion).Returns(VersionInfo.Parse("1.0.0"));

        _sut = new UpdateViewModel(_updateServiceMock.Object);
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_ShouldSetCurrentVersion()
    {
        // Assert
        _sut.CurrentVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void Constructor_ShouldInitializeWithNoUpdate()
    {
        // Assert
        _sut.HasUpdate.Should().BeFalse();
        _sut.IsChecking.Should().BeFalse();
        _sut.IsDownloading.Should().BeFalse();
    }

    #endregion

    #region CheckForUpdateCommand Tests

    [Fact]
    public async Task CheckForUpdateAsync_WhenUpdateAvailable_ShouldSetHasUpdate()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v2.0.0",
            Name = "Release 2.0.0",
            Body = "New features"
        };

        _updateServiceMock.Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.Available(VersionInfo.Parse("1.0.0"), releaseInfo));

        // Act
        await _sut.CheckForUpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.HasUpdate.Should().BeTrue();
        _sut.LatestVersion.Should().Be("2.0.0");
        _sut.ReleaseNotes.Should().Be("New features");
        _sut.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNoUpdate_ShouldSetStatusMessage()
    {
        // Arrange
        _updateServiceMock.Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.NoUpdate(VersionInfo.Parse("1.0.0")));

        // Act
        await _sut.CheckForUpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.HasUpdate.Should().BeFalse();
        _sut.StatusMessage.Should().Contain("最新版本");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenFailed_ShouldSetError()
    {
        // Arrange
        _updateServiceMock.Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.Failed(VersionInfo.Parse("1.0.0"), "Network error"));

        // Act
        await _sut.CheckForUpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.HasUpdate.Should().BeFalse();
        _sut.HasError.Should().BeTrue();
        _sut.StatusMessage.Should().Be("Network error");
    }

    #endregion

    #region DownloadUpdateCommand Tests

    [Fact]
    public void DownloadUpdateCommand_WhenNoUpdate_ShouldNotExecute()
    {
        // Assert
        _sut.DownloadUpdateCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenSuccessful_ShouldSetDownloadedFilePath()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v2.0.0",
            Assets =
            [
                new ReleaseAsset { Name = "app.zip", DownloadUrl = "https://example.com/app.zip" }
            ]
        };

        _updateServiceMock.Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.Available(VersionInfo.Parse("1.0.0"), releaseInfo));

        _updateServiceMock.Setup(x => x.DownloadUpdateAsync(
                It.IsAny<ReleaseInfo>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"C:\temp\app.zip");

        // First, check for update
        await _sut.CheckForUpdateCommand.ExecuteAsync(null);
        
        // 禁用自动安装，便于测试下载完成状态
        _sut.AutoInstallAfterDownload = false;

        // Act
        await _sut.DownloadUpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.DownloadedFilePath.Should().Be(@"C:\temp\app.zip");
        _sut.StatusMessage.Should().Contain("下载完成");
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenAutoInstallEnabled_ShouldCallInstall()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v2.0.0",
            Assets =
            [
                new ReleaseAsset { Name = "app.zip", DownloadUrl = "https://example.com/app.zip" }
            ]
        };

        _updateServiceMock.Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.Available(VersionInfo.Parse("1.0.0"), releaseInfo));

        _updateServiceMock.Setup(x => x.DownloadUpdateAsync(
                It.IsAny<ReleaseInfo>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"C:\temp\app.zip");

        // First, check for update
        await _sut.CheckForUpdateCommand.ExecuteAsync(null);
        
        // 启用自动安装
        _sut.AutoInstallAfterDownload = true;

        // Act
        await _sut.DownloadUpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.DownloadedFilePath.Should().Be(@"C:\temp\app.zip");
        // 自动安装会调用 LaunchInstallerAndExit
        _updateServiceMock.Verify(x => x.LaunchInstallerAndExit(@"C:\temp\app.zip"), Times.Once);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenFailed_ShouldSetError()
    {
        // Arrange
        var releaseInfo = new ReleaseInfo
        {
            TagName = "v2.0.0",
            Assets =
            [
                new ReleaseAsset { Name = "app.zip", DownloadUrl = "https://example.com/app.zip" }
            ]
        };

        _updateServiceMock.Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.Available(VersionInfo.Parse("1.0.0"), releaseInfo));

        _updateServiceMock.Setup(x => x.DownloadUpdateAsync(
                It.IsAny<ReleaseInfo>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.CheckForUpdateCommand.ExecuteAsync(null);

        // Act
        await _sut.DownloadUpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.DownloadedFilePath.Should().BeNull();
        _sut.HasError.Should().BeTrue();
    }

    #endregion

    #region InstallUpdateCommand Tests

    [Fact]
    public void InstallUpdateCommand_WhenNoDownloadedFile_ShouldNotExecute()
    {
        // Assert
        _sut.InstallUpdateCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public void Cleanup_ShouldNotThrow()
    {
        // Act & Assert
        var action = () => _sut.Cleanup();
        action.Should().NotThrow();
    }

    #endregion
}
