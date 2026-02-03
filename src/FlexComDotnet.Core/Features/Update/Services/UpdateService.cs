using System.Diagnostics;
using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 更新服务实现 - 整合版本检查、下载和安装功能
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly IVersionService _versionService;
    private readonly IGitHubReleaseService _releaseService;
    private readonly IDownloadService _downloadService;

    /// <summary>
    /// 更新检查进行中事件
    /// </summary>
    public event EventHandler<bool>? CheckingForUpdate;

    /// <summary>
    /// 下载状态变更事件
    /// </summary>
    public event EventHandler<DownloadStatus>? DownloadStatusChanged;

    /// <summary>
    /// 获取当前版本
    /// </summary>
    public VersionInfo CurrentVersion => _versionService.GetCurrentVersion();

    public UpdateService(
        IVersionService versionService,
        IGitHubReleaseService releaseService,
        IDownloadService downloadService)
    {
        _versionService = versionService;
        _releaseService = releaseService;
        _downloadService = downloadService;
    }

    /// <summary>
    /// 检查更新
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = _versionService.GetCurrentVersion();

        try
        {
            CheckingForUpdate?.Invoke(this, true);

            var latestRelease = await _releaseService.GetLatestReleaseAsync(cancellationToken);

            if (latestRelease is null)
            {
                return UpdateCheckResult.Failed(currentVersion, "无法获取最新版本信息，请检查网络连接。");
            }

            var isUpdateAvailable = _versionService.IsUpdateAvailable(latestRelease.Version);

            if (isUpdateAvailable)
            {
                return UpdateCheckResult.Available(currentVersion, latestRelease);
            }

            return UpdateCheckResult.NoUpdate(currentVersion);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(currentVersion, $"检查更新时发生错误: {ex.Message}");
        }
        finally
        {
            CheckingForUpdate?.Invoke(this, false);
        }
    }

    /// <summary>
    /// 下载更新包
    /// </summary>
    public async Task<string?> DownloadUpdateAsync(
        ReleaseInfo releaseInfo,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // 查找合适的下载资源 (优先 .zip，其次 .msix)
        var asset = FindDownloadAsset(releaseInfo.Assets);

        if (asset is null)
        {
            return null;
        }

        var downloadDir = _downloadService.GetDownloadDirectory();
        var destinationPath = Path.Combine(downloadDir, asset.Name);

        try
        {
            DownloadStatusChanged?.Invoke(this, DownloadStatus.Downloading);

            var success = await _downloadService.DownloadFileAsync(
                asset.DownloadUrl,
                destinationPath,
                progressCallback,
                cancellationToken);

            if (success)
            {
                DownloadStatusChanged?.Invoke(this, DownloadStatus.Completed);
                return destinationPath;
            }

            DownloadStatusChanged?.Invoke(this, DownloadStatus.Failed);
            return null;
        }
        catch (OperationCanceledException)
        {
            DownloadStatusChanged?.Invoke(this, DownloadStatus.Cancelled);
            return null;
        }
        catch (Exception)
        {
            DownloadStatusChanged?.Invoke(this, DownloadStatus.Failed);
            return null;
        }
    }

    /// <summary>
    /// 启动安装程序并退出当前应用
    /// </summary>
    public bool LaunchInstallerAndExit(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            return false;
        }

        try
        {
            var extension = Path.GetExtension(installerPath).ToLowerInvariant();

            ProcessStartInfo startInfo;

            if (extension == ".msix" || extension == ".msixbundle")
            {
                // 使用 Windows 安装程序打开 MSIX
                startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };
            }
            else if (extension == ".zip")
            {
                // 打开文件所在目录
                startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{installerPath}\"",
                    UseShellExecute = true
                };
            }
            else
            {
                // 默认使用 Shell 打开
                startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };
            }

            Process.Start(startInfo);

            // 退出当前应用程序
            Environment.Exit(0);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 查找合适的下载资源
    /// </summary>
    private static ReleaseAsset? FindDownloadAsset(IReadOnlyList<ReleaseAsset> assets)
    {
        if (assets.Count == 0)
        {
            return null;
        }

        // 优先查找 .zip 文件
        var zipAsset = assets.FirstOrDefault(a =>
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (zipAsset is not null)
        {
            return zipAsset;
        }

        // 其次查找 .msix 文件
        var msixAsset = assets.FirstOrDefault(a =>
            a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase));

        return msixAsset ?? assets.FirstOrDefault();
    }
}
