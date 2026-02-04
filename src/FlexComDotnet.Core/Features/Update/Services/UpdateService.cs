using System.Diagnostics;
using System.IO.Compression;
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

    /// <summary>
    /// 获取当前安装类型
    /// </summary>
    public InstallationType InstallationType => _versionService.GetInstallationType();

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
        // 根据当前安装类型查找合适的下载资源
        var installationType = _versionService.GetInstallationType();
        var asset = FindDownloadAsset(releaseInfo.Assets, installationType);

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

                Process.Start(startInfo);

                // 退出当前应用程序
                Environment.Exit(0);
            }
            else if (extension == ".zip")
            {
                // ZIP 便携版更新：解压并使用批处理脚本替换文件
                return ExtractAndUpdateFromZip(installerPath);
            }
            else
            {
                // 默认使用 Shell 打开
                startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // 退出当前应用程序
                Environment.Exit(0);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 从 ZIP 解压并更新程序
    /// </summary>
    private static bool ExtractAndUpdateFromZip(string zipPath)
    {
        try
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var tempExtractDir = Path.Combine(Path.GetTempPath(), "FlexComDotnet", "Update_" + Guid.NewGuid().ToString("N")[..8]);
            var batchScriptPath = Path.Combine(Path.GetTempPath(), "FlexComDotnet", $"update_{Guid.NewGuid():N}.bat");

            // 确保临时目录存在
            Directory.CreateDirectory(tempExtractDir);
            var batchDir = Path.GetDirectoryName(batchScriptPath);
            if (!string.IsNullOrEmpty(batchDir))
            {
                Directory.CreateDirectory(batchDir);
            }

            // 解压 ZIP 文件到临时目录
            ZipFile.ExtractToDirectory(zipPath, tempExtractDir, overwriteFiles: true);

            // 查找解压后的实际目录（可能有子目录）
            var extractedDirs = Directory.GetDirectories(tempExtractDir);
            var sourceDir = extractedDirs.Length == 1 ? extractedDirs[0] : tempExtractDir;

            // 创建更新批处理脚本
            var batchScript = $"""
                @echo off
                chcp 65001 > nul
                echo ========================================
                echo     FlexComDotnet 更新程序
                echo ========================================
                echo.
                echo 正在更新 FlexComDotnet...
                echo.
                
                :: 等待原程序退出
                timeout /t 2 /nobreak > nul
                
                :: 尝试多次等待，确保程序完全退出
                :waitloop
                tasklist /FI "IMAGENAME eq FlexComDotnet.exe" 2>nul | find /I "FlexComDotnet.exe" >nul
                if not errorlevel 1 (
                    echo 等待程序退出...
                    timeout /t 1 /nobreak > nul
                    goto waitloop
                )
                
                :: 备份当前版本（可选，保留最近一个备份）
                if exist "{appDirectory}backup" rd /s /q "{appDirectory}backup"
                
                :: 复制新文件到程序目录
                echo 正在复制更新文件...
                xcopy /E /Y /I /Q "{sourceDir}\*" "{appDirectory}"
                
                :: 清理临时文件
                echo 正在清理临时文件...
                rd /s /q "{tempExtractDir}" 2>nul
                del "{zipPath}" 2>nul
                
                echo.
                echo ========================================
                echo     更新完成!
                echo ========================================
                echo.
                
                :: 询问用户是否启动程序
                set /p choice=是否立即启动 FlexComDotnet? (Y/N): 
                if /i "%choice%"=="Y" (
                    echo 正在启动程序...
                    start "" "{Path.Combine(appDirectory, "FlexComDotnet.exe")}"
                ) else if /i "%choice%"=="y" (
                    echo 正在启动程序...
                    start "" "{Path.Combine(appDirectory, "FlexComDotnet.exe")}"
                ) else (
                    echo 您可以稍后手动启动程序。
                )
                
                echo.
                echo 按任意键关闭此窗口...
                pause > nul
                
                :: 删除自身
                del "%~f0"
                """;

            File.WriteAllText(batchScriptPath, batchScript, System.Text.Encoding.UTF8);

            // 启动批处理脚本
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchScriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

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
    /// 根据安装类型查找合适的下载资源
    /// </summary>
    private static ReleaseAsset? FindDownloadAsset(IReadOnlyList<ReleaseAsset> assets, InstallationType installationType)
    {
        if (assets.Count == 0)
        {
            return null;
        }

        // 根据安装类型选择对应的包
        return installationType switch
        {
            InstallationType.Msix => FindMsixAsset(assets) ?? FindZipAsset(assets) ?? assets.FirstOrDefault(),
            InstallationType.Portable => FindZipAsset(assets) ?? FindMsixAsset(assets) ?? assets.FirstOrDefault(),
            _ => FindZipAsset(assets) ?? FindMsixAsset(assets) ?? assets.FirstOrDefault()
        };
    }

    /// <summary>
    /// 查找 MSIX 资源
    /// </summary>
    private static ReleaseAsset? FindMsixAsset(IReadOnlyList<ReleaseAsset> assets)
    {
        return assets.FirstOrDefault(a =>
            a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 查找 ZIP 资源
    /// </summary>
    private static ReleaseAsset? FindZipAsset(IReadOnlyList<ReleaseAsset> assets)
    {
        return assets.FirstOrDefault(a =>
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }
}
