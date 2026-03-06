using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Visualization.Models;
using FlexComDotnet.Core.Features.Visualization.Services;

namespace FlexComDotnet.Core.Features.Visualization.ViewModels;

/// <summary>
/// 数据可视化 ViewModel
/// </summary>
public partial class DataVisualizationViewModel : ObservableObject
{
    private readonly IVisualizationService _visualizationService;
    private readonly IProtocolParserService _protocolParserService;
    private readonly SynchronizationContext? _syncContext;

    /// <summary>
    /// 预定义通道颜色列表
    /// </summary>
    private static readonly string[] DefaultColors =
    [
        "#2196F3", // Blue
        "#F44336", // Red
        "#4CAF50", // Green
        "#FF9800", // Orange
        "#9C27B0", // Purple
        "#00BCD4", // Cyan
        "#FFEB3B", // Yellow
        "#E91E63", // Pink
    ];

    private int _colorIndex;

    #region 属性

    /// <summary>
    /// 通道配置列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ChannelConfig> _channels = [];

    /// <summary>
    /// 可用的协议解析器列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableParsers = [];

    /// <summary>
    /// 选中的协议解析器名称
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? _selectedParserName;

    /// <summary>
    /// 选中的协议可用字段列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableFields = [];

    /// <summary>
    /// 选中的字段名称 (用于添加通道)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChannelCommand))]
    private string? _selectedFieldName;

    /// <summary>
    /// 选中的通道 (用于删除/配置)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveChannelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleChannelVisibilityCommand))]
    private ChannelConfig? _selectedChannel;

    /// <summary>
    /// 是否正在采集
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddChannelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveChannelCommand))]
    private bool _isRunning;

    /// <summary>
    /// 是否暂停波形显示（图形冻结但继续采集）
    /// </summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>
    /// 每通道最大数据点数
    /// </summary>
    [ObservableProperty]
    private int _maxDataPoints = 1000;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "就绪";

    /// <summary>
    /// 数据点总计
    /// </summary>
    [ObservableProperty]
    private long _totalDataPoints;

    #endregion

    /// <summary>
    /// 请求图表刷新事件（UI 层订阅）
    /// </summary>
    public event EventHandler? ChartRefreshRequested;

    /// <summary>
    /// 请求导出 PNG 事件（UI 层订阅）
    /// </summary>
    public event EventHandler<string>? ExportPngRequested;

    public DataVisualizationViewModel(
        IVisualizationService visualizationService,
        IProtocolParserService protocolParserService)
    {
        _visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));
        _protocolParserService = protocolParserService ?? throw new ArgumentNullException(nameof(protocolParserService));

        // 捕获 UI 线程的同步上下文
        _syncContext = SynchronizationContext.Current;

        // 订阅服务事件
        _visualizationService.DataPointAdded += OnDataPointAdded;
        _visualizationService.DataCleared += OnDataCleared;
        _visualizationService.StateChanged += OnStateChanged;
        _visualizationService.ExtractionFailed += OnExtractionFailed;

        // 订阅解析器注册事件
        _protocolParserService.ParserRegistered += OnParserRegistered;
        _protocolParserService.ParserRemoved += OnParserRemoved;

        // 加载可用解析器
        RefreshParsers();
    }

    #region 命令

    /// <summary>
    /// 开始采集
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        // 如果没有添加任何通道，自动为选中协议的所有启用字段添加通道
        if (Channels.Count == 0 && !string.IsNullOrEmpty(SelectedParserName))
        {
            AutoAddChannelsForSelectedParser();
        }

        _visualizationService.SelectedParserName = SelectedParserName;
        _visualizationService.MaxDataPoints = MaxDataPoints;
        _visualizationService.Start();
        StatusMessage = "采集中...";
    }

    private bool CanStart() => !IsRunning && !string.IsNullOrEmpty(SelectedParserName);

    /// <summary>
    /// 停止采集
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _visualizationService.Stop();
        StatusMessage = "已停止";
    }

    private bool CanStop() => IsRunning;

    /// <summary>
    /// 切换暂停/继续
    /// </summary>
    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        StatusMessage = IsPaused ? "波形已暂停 (数据继续采集)" : "采集中...";
    }

    /// <summary>
    /// 添加通道
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddChannel))]
    private void AddChannel()
    {
        if (string.IsNullOrEmpty(SelectedFieldName))
            return;

        // 检查是否已添加同名字段
        var existingChannels = _visualizationService.GetChannels();
        if (existingChannels.Any(c => c.FieldName == SelectedFieldName))
        {
            StatusMessage = $"字段 '{SelectedFieldName}' 已添加";
            return;
        }

        var channelId = $"ch_{Guid.NewGuid():N}";
        var color = DefaultColors[_colorIndex % DefaultColors.Length];
        _colorIndex++;

        var channel = new ChannelConfig
        {
            Id = channelId,
            FieldName = SelectedFieldName,
            DisplayName = SelectedFieldName,
            Color = color,
            IsVisible = true,
            Order = Channels.Count
        };

        _visualizationService.AddChannel(channel);
        Channels.Add(channel);
        StatusMessage = $"已添加通道: {channel.DisplayName}";
    }

    private bool CanAddChannel() => !string.IsNullOrEmpty(SelectedFieldName);

    /// <summary>
    /// 移除选中通道
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveChannel))]
    private void RemoveChannel()
    {
        if (SelectedChannel == null)
            return;

        var name = SelectedChannel.DisplayName;
        _visualizationService.RemoveChannel(SelectedChannel.Id);
        Channels.Remove(SelectedChannel);
        SelectedChannel = null;
        StatusMessage = $"已移除通道: {name}";
        ChartRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanRemoveChannel() => SelectedChannel != null;

    /// <summary>
    /// 切换通道可见性
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleChannelVisibility))]
    private void ToggleChannelVisibility()
    {
        if (SelectedChannel == null)
            return;

        SelectedChannel.IsVisible = !SelectedChannel.IsVisible;
        _visualizationService.UpdateChannel(SelectedChannel);
        ChartRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanToggleChannelVisibility() => SelectedChannel != null;

    /// <summary>
    /// 清除所有数据
    /// </summary>
    [RelayCommand]
    private void ClearData()
    {
        _visualizationService.ClearData();
        TotalDataPoints = 0;
        StatusMessage = "数据已清除";
    }

    /// <summary>
    /// 导出 CSV 数据
    /// </summary>
    [RelayCommand]
    private void ExportCsv(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            _visualizationService.ExportToCsv(filePath);
            StatusMessage = $"数据已导出: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 导出 PNG 图片
    /// </summary>
    [RelayCommand]
    private void ExportPng(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        ExportPngRequested?.Invoke(this, filePath);
        StatusMessage = $"图片已导出: {Path.GetFileName(filePath)}";
    }

    /// <summary>
    /// 刷新可用解析器列表
    /// </summary>
    [RelayCommand]
    private void RefreshParsers()
    {
        AvailableParsers.Clear();
        foreach (var parser in _protocolParserService.GetAllParsers())
        {
            AvailableParsers.Add(parser.Name);
        }
    }

    #endregion

    #region 属性变更处理

    partial void OnSelectedParserNameChanged(string? value)
    {
        RefreshAvailableFields();
    }

    partial void OnMaxDataPointsChanged(int value)
    {
        _visualizationService.MaxDataPoints = value;
    }

    /// <summary>
    /// 刷新选中协议可用的字段列表
    /// </summary>
    private void RefreshAvailableFields()
    {
        AvailableFields.Clear();

        if (string.IsNullOrEmpty(SelectedParserName))
            return;

        var parser = _protocolParserService.GetParser(SelectedParserName);
        if (parser?.Definition?.Fields == null)
            return;

        foreach (var field in parser.Definition.Fields.Where(f => f.IsEnabled))
        {
            AvailableFields.Add(field.Name);
        }
    }

    /// <summary>
    /// 自动为选中协议的所有启用字段添加通道
    /// </summary>
    private void AutoAddChannelsForSelectedParser()
    {
        var parser = _protocolParserService.GetParser(SelectedParserName!);
        if (parser?.Definition?.Fields == null)
            return;

        foreach (var field in parser.Definition.Fields.Where(f => f.IsEnabled))
        {
            var channelId = $"ch_{Guid.NewGuid():N}";
            var color = DefaultColors[_colorIndex % DefaultColors.Length];
            _colorIndex++;

            var channel = new ChannelConfig
            {
                Id = channelId,
                FieldName = field.Name,
                DisplayName = field.Name,
                Color = color,
                IsVisible = true,
                Order = Channels.Count
            };

            _visualizationService.AddChannel(channel);
            Channels.Add(channel);
        }
    }

    #endregion

    #region 事件处理

    private void OnDataPointAdded(object? sender, DataPointAddedEventArgs e)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ =>
            {
                TotalDataPoints++;
                if (!IsPaused)
                {
                    ChartRefreshRequested?.Invoke(this, EventArgs.Empty);
                }
            }, null);
        }
        else
        {
            TotalDataPoints++;
            if (!IsPaused)
            {
                ChartRefreshRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnDataCleared(object? sender, EventArgs e)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ =>
            {
                TotalDataPoints = 0;
                ChartRefreshRequested?.Invoke(this, EventArgs.Empty);
            }, null);
        }
        else
        {
            TotalDataPoints = 0;
            ChartRefreshRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnStateChanged(object? sender, VisualizationStateChangedEventArgs e)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => IsRunning = e.IsRunning, null);
        }
        else
        {
            IsRunning = e.IsRunning;
        }
    }

    private void OnExtractionFailed(object? sender, ExtractionFailedEventArgs e)
    {
        void DoUpdate()
        {
            StatusMessage = $"帧提取失败 - 已接收 {e.BytesReceived} 字节但无法匹配协议，请检查协议配置（帧头/帧尾）是否与接收数据匹配";
        }

        if (_syncContext != null)
            _syncContext.Post(_ => DoUpdate(), null);
        else
            DoUpdate();
    }

    private void OnParserRegistered(object? sender, ParserRegisteredEventArgs e)
    {
        void DoAdd()
        {
            if (!AvailableParsers.Contains(e.Parser.Name))
            {
                AvailableParsers.Add(e.Parser.Name);
            }
        }

        if (_syncContext != null)
            _syncContext.Post(_ => DoAdd(), null);
        else
            DoAdd();
    }

    private void OnParserRemoved(object? sender, ParserRemovedEventArgs e)
    {
        void DoRemove()
        {
            AvailableParsers.Remove(e.ParserName);
            if (SelectedParserName == e.ParserName)
            {
                SelectedParserName = null;
            }
        }

        if (_syncContext != null)
            _syncContext.Post(_ => DoRemove(), null);
        else
            DoRemove();
    }

    #endregion

    #region 公共方法 (供 View 层调用)

    /// <summary>
    /// 获取指定通道的数据点（供图表控件使用）
    /// </summary>
    public IReadOnlyList<ChartDataPoint> GetChannelData(string channelId)
    {
        return _visualizationService.GetChannelData(channelId);
    }

    /// <summary>
    /// 获取所有通道数据（供图表控件使用）
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ChartDataPoint>> GetAllData()
    {
        return _visualizationService.GetAllData();
    }

    /// <summary>
    /// 推送解析帧到可视化服务（供通信层调用）
    /// </summary>
    public void PushParsedFrame(ParsedFrame frame)
    {
        _visualizationService.PushData(frame);
    }

    #endregion
}
