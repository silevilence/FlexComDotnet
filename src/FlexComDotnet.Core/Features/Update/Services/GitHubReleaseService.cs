using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// GitHub Release 服务实现
/// </summary>
public class GitHubReleaseService : IGitHubReleaseService
{
    private const string Owner = "silevilence";
    private const string Repo = "FlexComDotnet";
    private const string BaseUrl = "https://api.github.com";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public GitHubReleaseService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// 获取最新 Release 信息
    /// </summary>
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/repos/{Owner}/{Repo}/releases/latest";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto = JsonSerializer.Deserialize<GitHubReleaseDto>(content, _jsonOptions);

            return dto?.ToReleaseInfo();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取所有 Release 列表
    /// </summary>
    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/repos/{Owner}/{Repo}/releases";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var dtos = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(content, _jsonOptions);

            if (dtos is null)
            {
                return [];
            }

            var releases = dtos.Select(d => d.ToReleaseInfo()).ToList();

            if (!includePrerelease)
            {
                releases = releases.Where(r => !r.IsPrerelease).ToList();
            }

            return releases;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// 创建默认的 HttpClient
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FlexComDotnet-Updater");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        return client;
    }

    #region DTOs for JSON Deserialization

    private record GitHubReleaseDto
    {
        public string TagName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public DateTime PublishedAt { get; init; }
        public bool Prerelease { get; init; }
        public string HtmlUrl { get; init; } = string.Empty;
        public List<GitHubAssetDto> Assets { get; init; } = [];

        public ReleaseInfo ToReleaseInfo() => new()
        {
            TagName = TagName,
            Name = Name,
            Body = Body,
            PublishedAt = PublishedAt,
            IsPrerelease = Prerelease,
            HtmlUrl = HtmlUrl,
            Assets = Assets.Select(a => a.ToReleaseAsset()).ToList()
        };
    }

    private record GitHubAssetDto
    {
        public string Name { get; init; } = string.Empty;
        public string BrowserDownloadUrl { get; init; } = string.Empty;
        public long Size { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public int DownloadCount { get; init; }

        public ReleaseAsset ToReleaseAsset() => new()
        {
            Name = Name,
            DownloadUrl = BrowserDownloadUrl,
            Size = Size,
            ContentType = ContentType,
            DownloadCount = DownloadCount
        };
    }

    #endregion
}
