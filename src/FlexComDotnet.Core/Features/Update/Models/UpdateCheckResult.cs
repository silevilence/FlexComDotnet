namespace FlexComDotnet.Core.Features.Update.Models;

/// <summary>
/// 更新检查结果
/// </summary>
public record UpdateCheckResult
{
    /// <summary>
    /// 是否有新版本可用
    /// </summary>
    public bool HasUpdate { get; init; }

    /// <summary>
    /// 当前版本
    /// </summary>
    public VersionInfo CurrentVersion { get; init; } = VersionInfo.Empty;

    /// <summary>
    /// 最新版本 (如果有更新)
    /// </summary>
    public VersionInfo? LatestVersion { get; init; }

    /// <summary>
    /// Release 信息 (如果有更新)
    /// </summary>
    public ReleaseInfo? ReleaseInfo { get; init; }

    /// <summary>
    /// 检查是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 错误信息 (如果检查失败)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功的无更新结果
    /// </summary>
    public static UpdateCheckResult NoUpdate(VersionInfo currentVersion) => new()
    {
        HasUpdate = false,
        CurrentVersion = currentVersion,
        IsSuccess = true
    };

    /// <summary>
    /// 创建成功的有更新结果
    /// </summary>
    public static UpdateCheckResult Available(VersionInfo currentVersion, ReleaseInfo releaseInfo) => new()
    {
        HasUpdate = true,
        CurrentVersion = currentVersion,
        LatestVersion = releaseInfo.Version,
        ReleaseInfo = releaseInfo,
        IsSuccess = true
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static UpdateCheckResult Failed(VersionInfo currentVersion, string errorMessage) => new()
    {
        HasUpdate = false,
        CurrentVersion = currentVersion,
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
