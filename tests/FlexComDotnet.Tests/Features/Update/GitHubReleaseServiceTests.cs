using FlexComDotnet.Core.Features.Update.Models;
using FlexComDotnet.Core.Features.Update.Services;
using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace FlexComDotnet.Tests.Features.Update;

/// <summary>
/// GitHubReleaseService 测试
/// </summary>
public class GitHubReleaseServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly GitHubReleaseService _sut;

    public GitHubReleaseServiceTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
        _sut = new GitHubReleaseService(_httpClient);
    }

    #region GetLatestReleaseAsync Tests

    [Fact]
    public async Task GetLatestReleaseAsync_WithValidResponse_ShouldReturnReleaseInfo()
    {
        // Arrange
        var responseJson = """
        {
            "tag_name": "v1.2.3",
            "name": "Release 1.2.3",
            "body": "## What's New\n- Feature A\n- Fix B",
            "published_at": "2024-01-15T10:30:00Z",
            "prerelease": false,
            "html_url": "https://github.com/silevilence/FlexComDotnet/releases/tag/v1.2.3",
            "assets": [
                {
                    "name": "FlexComDotnet_1.2.3.zip",
                    "browser_download_url": "https://github.com/silevilence/FlexComDotnet/releases/download/v1.2.3/FlexComDotnet_1.2.3.zip",
                    "size": 15728640,
                    "content_type": "application/zip",
                    "download_count": 50
                }
            ]
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _sut.GetLatestReleaseAsync();

        // Assert
        result.Should().NotBeNull();
        result!.TagName.Should().Be("v1.2.3");
        result.Name.Should().Be("Release 1.2.3");
        result.Body.Should().Contain("What's New");
        result.IsPrerelease.Should().BeFalse();
        result.Assets.Should().HaveCount(1);
        result.Assets[0].Name.Should().Be("FlexComDotnet_1.2.3.zip");
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WithNetworkError_ShouldReturnNull()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.GetLatestReleaseAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WithNotFoundResponse_ShouldReturnNull()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.NotFound, "");

        // Act
        var result = await _sut.GetLatestReleaseAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "invalid json");

        // Act
        var result = await _sut.GetLatestReleaseAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetReleasesAsync Tests

    [Fact]
    public async Task GetReleasesAsync_WithValidResponse_ShouldReturnReleaseList()
    {
        // Arrange
        var responseJson = """
        [
            {
                "tag_name": "v1.2.0",
                "name": "Release 1.2.0",
                "body": "Changes",
                "published_at": "2024-01-10T10:00:00Z",
                "prerelease": false,
                "html_url": "https://github.com/silevilence/FlexComDotnet/releases/tag/v1.2.0",
                "assets": []
            },
            {
                "tag_name": "v1.1.0",
                "name": "Release 1.1.0",
                "body": "Old changes",
                "published_at": "2024-01-05T10:00:00Z",
                "prerelease": false,
                "html_url": "https://github.com/silevilence/FlexComDotnet/releases/tag/v1.1.0",
                "assets": []
            }
        ]
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _sut.GetReleasesAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].TagName.Should().Be("v1.2.0");
        result[1].TagName.Should().Be("v1.1.0");
    }

    [Fact]
    public async Task GetReleasesAsync_ExcludePrerelease_ShouldFilterResults()
    {
        // Arrange
        var responseJson = """
        [
            {
                "tag_name": "v2.0.0-beta",
                "name": "Release 2.0.0 Beta",
                "body": "Beta",
                "published_at": "2024-01-15T10:00:00Z",
                "prerelease": true,
                "html_url": "https://example.com",
                "assets": []
            },
            {
                "tag_name": "v1.0.0",
                "name": "Release 1.0.0",
                "body": "Stable",
                "published_at": "2024-01-10T10:00:00Z",
                "prerelease": false,
                "html_url": "https://example.com",
                "assets": []
            }
        ]
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _sut.GetReleasesAsync(includePrerelease: false);

        // Assert
        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("v1.0.0");
    }

    [Fact]
    public async Task GetReleasesAsync_IncludePrerelease_ShouldReturnAll()
    {
        // Arrange
        var responseJson = """
        [
            {
                "tag_name": "v2.0.0-beta",
                "name": "Release 2.0.0 Beta",
                "body": "Beta",
                "published_at": "2024-01-15T10:00:00Z",
                "prerelease": true,
                "html_url": "https://example.com",
                "assets": []
            },
            {
                "tag_name": "v1.0.0",
                "name": "Release 1.0.0",
                "body": "Stable",
                "published_at": "2024-01-10T10:00:00Z",
                "prerelease": false,
                "html_url": "https://example.com",
                "assets": []
            }
        ]
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _sut.GetReleasesAsync(includePrerelease: true);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    #endregion
}
