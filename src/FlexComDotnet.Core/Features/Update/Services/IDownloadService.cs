using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 下载服务接口
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url">下载 URL</param>
    /// <param name="destinationPath">目标文件路径</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载是否成功</returns>
    Task<bool> DownloadFileAsync(
        string url,
        string destinationPath,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取临时下载目录
    /// </summary>
    /// <returns>临时目录路径</returns>
    string GetDownloadDirectory();
}
