using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;

namespace FlexComDotnet.Core.Features.Settings.ViewModels;

/// <summary>
/// 调试工具窗口 ViewModel
/// </summary>
public partial class DebugToolsViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;

    [ObservableProperty]
    private LogSource _selectedSource = LogSource.System;

    [ObservableProperty]
    private LogLevel _selectedLevel = LogLevel.Info;

    [ObservableProperty]
    private string _logContent = "test log";

    public DebugToolsViewModel(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// 可选的日志来源列表
    /// </summary>
    public IReadOnlyList<LogSource> AvailableSources { get; } =
        Enum.GetValues<LogSource>();

    /// <summary>
    /// 可选的日志等级列表
    /// </summary>
    public IReadOnlyList<LogLevel> AvailableLevels { get; } =
        Enum.GetValues<LogLevel>();

    /// <summary>
    /// 发送日志
    /// </summary>
    [RelayCommand]
    private void SendLog()
    {
        var content = string.IsNullOrWhiteSpace(LogContent) ? "test log" : LogContent;
        _loggingService.Log(SelectedLevel, SelectedSource, content);
    }
}
