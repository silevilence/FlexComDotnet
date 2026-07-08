using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using FlexComDotnet.Core.Features.Driver.Services;
using FlexComDotnet.Core.Features.Layout.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Settings.Models;
using FlexComDotnet.Core.Features.Update.Services;

namespace FlexComDotnet.Core.Features.Settings.ViewModels;

/// <summary>
/// 设置窗口 ViewModel
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly IVersionService _versionService;
    private readonly IPanelManager _panelManager;
    private readonly DriverLifecycleService? _driverLifecycleService;
    private readonly Action _restartAsAdministrator;
    private readonly string _logDirectory;

    [ObservableProperty]
    private bool _isDebugModeEnabled;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private string _driverOperationStatus = "驱动未自动安装；如需串口监控，请手动安装/启动。";

    public SettingsViewModel(
        IConfigurationService configService,
        IVersionService versionService,
        IPanelManager panelManager,
        string logDirectory,
        DriverLifecycleService? driverLifecycleService = null,
        Action? restartAsAdministrator = null)
    {
        _configService = configService;
        _versionService = versionService;
        _panelManager = panelManager;
        _driverLifecycleService = driverLifecycleService;
        _restartAsAdministrator = restartAsAdministrator ?? RestartCurrentProcessAsAdministrator;
        _logDirectory = logDirectory;

        LoadSettings();
    }

    /// <summary>
    /// 获取面板列表（用于面板管理）
    /// </summary>
    public IEnumerable<PanelVisibilityItem> PanelItems =>
        _panelManager.Panels
            .Where(p => p.Id != "connection-config")
            .Select(p => new PanelVisibilityItem(p.Id, p.Title, p.IsVisible));

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        var config = _configService.Load();
        IsDebugModeEnabled = config.DebugConfig.IsDebugModeEnabled;

        var versionInfo = _versionService.GetCurrentVersion();
        CurrentVersion = $"v{versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Patch}";
    }

    /// <summary>
    /// 保存调试模式设置
    /// </summary>
    partial void OnIsDebugModeEnabledChanged(bool value)
    {
        var config = _configService.Load();
        config.DebugConfig.IsDebugModeEnabled = value;
        _configService.Save(config);
    }

    /// <summary>
    /// 获取日志目录路径
    /// </summary>
    public string LogDirectory => _logDirectory;

    /// <summary>
    /// 切换面板可见性
    /// </summary>
    [RelayCommand]
    private void TogglePanelVisibility(string panelId)
    {
        PanelVisibilityToggled?.Invoke(this, panelId);
        OnPropertyChanged(nameof(PanelItems));
    }

    /// <summary>
    /// 面板可见性切换事件（由 View 层处理实际逻辑）
    /// </summary>
    public event EventHandler<string>? PanelVisibilityToggled;

    [RelayCommand]
    private void InstallDriver() =>
        RunDriverOperation(static service => service.EnsureInstalledAndStarted());

    [RelayCommand]
    private void StopDriver() =>
        RunDriverOperation(static service => service.StopDriver());

    [RelayCommand]
    private void UninstallDriver() =>
        RunDriverOperation(static service => service.UninstallDriver());

    [RelayCommand]
    private void RestartAsAdministrator()
    {
        try
        {
            _restartAsAdministrator();
            DriverOperationStatus = "已请求以管理员身份重启。";
        }
        catch (Exception ex)
        {
            DriverOperationStatus = $"管理员重启失败：{ex.Message}";
        }
    }

    private void RunDriverOperation(Func<DriverLifecycleService, DriverLifecycleResult> operation)
    {
        DriverOperationStatus = _driverLifecycleService is null
            ? "驱动服务未配置。"
            : operation(_driverLifecycleService).Message;
    }

    private static void RestartCurrentProcessAsAdministrator()
    {
        var fileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("无法定位当前程序路径。");
        }

        Process.Start(new ProcessStartInfo(fileName)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        });
        Environment.Exit(0);
    }
}

/// <summary>
/// 面板可见性项
/// </summary>
public class PanelVisibilityItem
{
    public string Id { get; }
    public string Title { get; }
    public bool IsVisible { get; }

    public PanelVisibilityItem(string id, string title, bool isVisible)
    {
        Id = id;
        Title = title;
        IsVisible = isVisible;
    }
}
