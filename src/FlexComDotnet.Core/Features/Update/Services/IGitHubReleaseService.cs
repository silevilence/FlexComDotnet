using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// GitHub Release 服务接口
/// </summary>
public interface IGitHubReleaseService
{
    /// <summary>
    /// 获取最新 Release 信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最新 Release 信息，如果获取失败返回 null</returns>
    Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有 Release 列表
    /// </summary>
    /// <param name="includePrerelease">是否包含预发布版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Release 列表</returns>
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(bool includePrerelease = false, CancellationToken cancellationToken = default);
}
