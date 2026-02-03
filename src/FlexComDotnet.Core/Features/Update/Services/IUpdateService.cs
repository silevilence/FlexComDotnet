using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 更新服务接口 - 整合版本检查、下载和安装功能
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 检查更新
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新检查结果</returns>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载更新包
    /// </summary>
    /// <param name="releaseInfo">Release 信息</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载的文件路径，失败返回 null</returns>
    Task<string?> DownloadUpdateAsync(
        ReleaseInfo releaseInfo,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动安装程序并退出当前应用
    /// </summary>
    /// <param name="installerPath">安装程序路径</param>
    /// <returns>是否成功启动安装程序</returns>
    bool LaunchInstallerAndExit(string installerPath);

    /// <summary>
    /// 获取当前版本
    /// </summary>
    VersionInfo CurrentVersion { get; }

    /// <summary>
    /// 更新检查进行中事件
    /// </summary>
    event EventHandler<bool>? CheckingForUpdate;

    /// <summary>
    /// 下载状态变更事件
    /// </summary>
    event EventHandler<DownloadStatus>? DownloadStatusChanged;
}
