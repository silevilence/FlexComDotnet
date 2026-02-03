namespace FlexComDotnet.Core.Features.Update.Models;

/// <summary>
/// GitHub Release 信息
/// </summary>
public record ReleaseInfo
{
    /// <summary>
    /// 版本标签 (如 v1.0.0)
    /// </summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>
    /// 发布名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 发布说明 (Release Notes)
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime PublishedAt { get; init; }

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    public bool IsPrerelease { get; init; }

    /// <summary>
    /// HTML 页面 URL
    /// </summary>
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>
    /// 附件列表
    /// </summary>
    public IReadOnlyList<ReleaseAsset> Assets { get; init; } = [];

    /// <summary>
    /// 解析后的版本信息
    /// </summary>
    public VersionInfo Version => VersionInfo.Parse(TagName);
}

/// <summary>
/// Release 附件信息
/// </summary>
public record ReleaseAsset
{
    /// <summary>
    /// 附件名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 下载 URL
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>
    /// 文件大小 (字节)
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// 下载次数
    /// </summary>
    public int DownloadCount { get; init; }
}
