using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Update.Models;
using FlexComDotnet.Core.Features.Update.Services;

namespace FlexComDotnet.Core.Features.Update.ViewModels;

/// <summary>
/// 更新视图模型
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    private readonly IUpdateService _updateService;
    private CancellationTokenSource? _downloadCts;

    #region Observable Properties

    /// <summary>
    /// 当前版本
    /// </summary>
    [ObservableProperty]
    private string _currentVersion = string.Empty;

    /// <summary>
    /// 最新版本
    /// </summary>
    [ObservableProperty]
    private string _latestVersion = string.Empty;

    /// <summary>
    /// 是否有可用更新
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    private bool _hasUpdate;

    /// <summary>
    /// 是否正在检查更新
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdateCommand))]
    private bool _isChecking;

    /// <summary>
    /// 是否正在下载
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDownloadCommand))]
    private bool _isDownloading;

    /// <summary>
    /// 下载进度 (0-100)
    /// </summary>
    [ObservableProperty]
    private double _downloadProgress;

    /// <summary>
    /// 下载进度文本
    /// </summary>
    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    /// <summary>
    /// 下载速度文本
    /// </summary>
    [ObservableProperty]
    private string _downloadSpeedText = string.Empty;

    /// <summary>
    /// Release 说明
    /// </summary>
    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 是否显示错误状态
    /// </summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// 已下载的文件路径
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private string? _downloadedFilePath;

    /// <summary>
    /// 下载完成后是否自动安装
    /// </summary>
    [ObservableProperty]
    private bool _autoInstallAfterDownload = true;

    #endregion

    /// <summary>
    /// 当前 Release 信息
    /// </summary>
    private ReleaseInfo? _currentRelease;

    /// <summary>
    /// 有可用更新事件（用于通知 UI 显示更新提示）
    /// </summary>
    public event EventHandler? UpdateAvailable;

    public UpdateViewModel(IUpdateService updateService)
    {
        _updateService = updateService;

        // 订阅事件
        _updateService.CheckingForUpdate += OnCheckingForUpdate;
        _updateService.DownloadStatusChanged += OnDownloadStatusChanged;

        // 初始化当前版本
        CurrentVersion = _updateService.CurrentVersion.ToString();
        
        // 初始化安装类型
        InstallationType = _updateService.InstallationType;
        InstallationTypeText = InstallationType switch
        {
            Models.InstallationType.Msix => "MSIX 安装版",
            Models.InstallationType.Portable => "ZIP 便携版",
            _ => "未知"
        };
    }

    /// <summary>
    /// 当前安装类型
    /// </summary>
    public InstallationType InstallationType { get; }

    /// <summary>
    /// 安装类型显示文本
    /// </summary>
    public string InstallationTypeText { get; }

    /// <summary>
    /// 后台静默检查更新（不更新 UI 状态消息）
    /// </summary>
    public async Task CheckForUpdateSilentlyAsync()
    {
        try
        {
            var result = await _updateService.CheckForUpdateAsync();

            if (result.IsSuccess && result.HasUpdate && result.ReleaseInfo is not null)
            {
                _currentRelease = result.ReleaseInfo;
                LatestVersion = result.LatestVersion!.ToString();
                ReleaseNotes = result.ReleaseInfo.Body;
                HasUpdate = true;
                
                // 触发更新可用事件
                UpdateAvailable?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // 静默检查，忽略错误
        }
    }

    #region Commands

    /// <summary>
    /// 检查更新命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
    private async Task CheckForUpdateAsync()
    {
        HasError = false;
        HasUpdate = false;
        StatusMessage = "正在检查更新...";
        ReleaseNotes = string.Empty;

        var result = await _updateService.CheckForUpdateAsync();

        if (!result.IsSuccess)
        {
            HasError = true;
            StatusMessage = result.ErrorMessage ?? "检查更新失败";
            return;
        }

        if (result.HasUpdate && result.ReleaseInfo is not null)
        {
            _currentRelease = result.ReleaseInfo;
            LatestVersion = result.LatestVersion!.ToString();
            ReleaseNotes = result.ReleaseInfo.Body;
            StatusMessage = $"发现新版本: {LatestVersion}";
            HasUpdate = true;  // 最后设置，触发命令 CanExecute 重新评估
        }
        else
        {
            _currentRelease = null;
            StatusMessage = "当前已是最新版本";
        }
    }

    private bool CanCheckForUpdate() => !IsChecking && !IsDownloading;

    /// <summary>
    /// 下载更新命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
    private async Task DownloadUpdateAsync()
    {
        if (_currentRelease is null)
        {
            return;
        }

        HasError = false;
        DownloadProgress = 0;
        DownloadProgressText = string.Empty;
        DownloadSpeedText = string.Empty;
        DownloadedFilePath = null;

        _downloadCts = new CancellationTokenSource();

        try
        {
            var filePath = await _updateService.DownloadUpdateAsync(
                _currentRelease,
                OnDownloadProgress,
                _downloadCts.Token);

            if (filePath is not null)
            {
                DownloadedFilePath = filePath;
                
                // 如果启用了自动安装，则自动执行安装
                if (AutoInstallAfterDownload)
                {
                    StatusMessage = "下载完成，正在启动安装...";
                    InstallUpdate();
                }
                else
                {
                    StatusMessage = "下载完成，点击安装按钮开始安装";
                }
            }
            else
            {
                HasError = true;
                StatusMessage = "下载失败，请稍后重试";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "下载已取消";
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private bool CanDownloadUpdate() => HasUpdate && !IsDownloading && _currentRelease is not null;

    /// <summary>
    /// 取消下载命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelDownload))]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    private bool CanCancelDownload() => IsDownloading;

    /// <summary>
    /// 安装更新命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private void InstallUpdate()
    {
        if (string.IsNullOrEmpty(DownloadedFilePath))
        {
            return;
        }

        var success = _updateService.LaunchInstallerAndExit(DownloadedFilePath);

        if (!success)
        {
            HasError = true;
            StatusMessage = "无法启动安装程序";
        }
    }

    private bool CanInstallUpdate() => !string.IsNullOrEmpty(DownloadedFilePath);

    /// <summary>
    /// 打开 Release 页面命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenReleasePage))]
    private void OpenReleasePage()
    {
        if (_currentRelease is null || string.IsNullOrEmpty(_currentRelease.HtmlUrl))
        {
            return;
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _currentRelease.HtmlUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch
        {
            // 忽略打开失败
        }
    }

    private bool CanOpenReleasePage() => _currentRelease is not null && !string.IsNullOrEmpty(_currentRelease.HtmlUrl);

    #endregion

    #region Event Handlers

    private void OnCheckingForUpdate(object? sender, bool isChecking)
    {
        IsChecking = isChecking;
        CheckForUpdateCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
    }

    private void OnDownloadStatusChanged(object? sender, DownloadStatus status)
    {
        IsDownloading = status == DownloadStatus.Downloading;

        CheckForUpdateCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        CancelDownloadCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    private void OnDownloadProgress(DownloadProgress progress)
    {
        DownloadProgress = progress.ProgressPercentage;
        DownloadProgressText = $"{progress.FormattedBytesReceived} / {progress.FormattedTotalBytes ?? "未知"}";
        DownloadSpeedText = progress.FormattedSpeed;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Cleanup()
    {
        _updateService.CheckingForUpdate -= OnCheckingForUpdate;
        _updateService.DownloadStatusChanged -= OnDownloadStatusChanged;
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
    }

    #endregion
}
