using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.AutoReply.ViewModels;

/// <summary>
/// 自动回复功能 ViewModel
/// </summary>
public partial class AutoReplyViewModel : ObservableObject, IDisposable
{
    private readonly IAutoReplyService _autoReplyService;
    private readonly IConfigurationService _configurationService;
    private bool _disposed;

    #region Observable Properties

    /// <summary>
    /// 全局回复延迟（毫秒）
    /// </summary>
    [ObservableProperty]
    private int _globalDelayMs = 100;

    /// <summary>
    /// 当前激活的回复模式
    /// </summary>
    [ObservableProperty]
    private ReplyMode _activeMode = ReplyMode.Match;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// 接收计数
    /// </summary>
    [ObservableProperty]
    private int _receiveCount;

    /// <summary>
    /// 回复计数
    /// </summary>
    [ObservableProperty]
    private int _replyCount;

    /// <summary>
    /// 匹配规则列表
    /// </summary>
    public ObservableCollection<MatchRuleViewModel> MatchRules { get; } = [];

    /// <summary>
    /// 顺序帧列表
    /// </summary>
    public ObservableCollection<SequentialFrameViewModel> SequentialFrames { get; } = [];

    /// <summary>
    /// 是否启用循环
    /// </summary>
    [ObservableProperty]
    private bool _enableLoop = true;

    /// <summary>
    /// 当前顺序帧索引
    /// </summary>
    [ObservableProperty]
    private int _currentFrameIndex;

    /// <summary>
    /// 回复日志
    /// </summary>
    public ObservableCollection<ReplyLogEntry> ReplyLogs { get; } = [];

    /// <summary>
    /// 当前选中的匹配规则
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveMatchRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMatchRuleUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMatchRuleDownCommand))]
    private MatchRuleViewModel? _selectedMatchRule;

    /// <summary>
    /// 当前选中的顺序帧
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSequentialFrameCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSequentialFrameUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSequentialFrameDownCommand))]
    private SequentialFrameViewModel? _selectedSequentialFrame;

    #endregion

    #region Commands

    /// <summary>
    /// 切换运行状态命令
    /// </summary>
    [RelayCommand]
    private void ToggleRunning()
    {
        if (IsRunning)
        {
            _autoReplyService.Stop();
            IsRunning = false;
        }
        else
        {
            SyncToService();
            _autoReplyService.Start();
            IsRunning = true;
        }
        AutoSave();
    }

    /// <summary>
    /// 重置计数命令
    /// </summary>
    [RelayCommand]
    private void ResetCounters()
    {
        _autoReplyService.ResetCounters();
        ReceiveCount = 0;
        ReplyCount = 0;
    }

    /// <summary>
    /// 重置顺序索引命令
    /// </summary>
    [RelayCommand]
    private void ResetSequenceIndex()
    {
        _autoReplyService.ResetHandlerState();
        CurrentFrameIndex = 0;
    }

    /// <summary>
    /// 添加匹配规则命令
    /// </summary>
    [RelayCommand]
    private void AddMatchRule()
    {
        var rule = new MatchRuleViewModel
        {
            Id = GetNextMatchRuleId(),
            Name = $"规则 {MatchRules.Count + 1}",
            SortOrder = MatchRules.Count,
            IsEnabled = true
        };
        MatchRules.Add(rule);
        SelectedMatchRule = rule;
        AutoSave();
    }

    /// <summary>
    /// 移除匹配规则命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyMatchRule))]
    private void RemoveMatchRule()
    {
        if (SelectedMatchRule != null)
        {
            MatchRules.Remove(SelectedMatchRule);
            UpdateMatchRuleSortOrders();
            AutoSave();
        }
    }

    private bool CanModifyMatchRule() => SelectedMatchRule != null;

    /// <summary>
    /// 上移匹配规则命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveMatchRuleUp))]
    private void MoveMatchRuleUp()
    {
        if (SelectedMatchRule == null) return;
        var index = MatchRules.IndexOf(SelectedMatchRule);
        if (index > 0)
        {
            MatchRules.Move(index, index - 1);
            UpdateMatchRuleSortOrders();
            AutoSave();
        }
    }

    private bool CanMoveMatchRuleUp() => SelectedMatchRule != null && MatchRules.IndexOf(SelectedMatchRule) > 0;

    /// <summary>
    /// 下移匹配规则命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveMatchRuleDown))]
    private void MoveMatchRuleDown()
    {
        if (SelectedMatchRule == null) return;
        var index = MatchRules.IndexOf(SelectedMatchRule);
        if (index >= 0 && index < MatchRules.Count - 1)
        {
            MatchRules.Move(index, index + 1);
            UpdateMatchRuleSortOrders();
            AutoSave();
        }
    }

    private bool CanMoveMatchRuleDown() => SelectedMatchRule != null && MatchRules.IndexOf(SelectedMatchRule) < MatchRules.Count - 1;

    /// <summary>
    /// 添加顺序帧命令
    /// </summary>
    [RelayCommand]
    private void AddSequentialFrame()
    {
        var frame = new SequentialFrameViewModel
        {
            Id = GetNextSequentialFrameId(),
            Name = $"帧 {SequentialFrames.Count + 1}",
            SortOrder = SequentialFrames.Count,
            IsEnabled = true
        };
        SequentialFrames.Add(frame);
        SelectedSequentialFrame = frame;
        AutoSave();
    }

    /// <summary>
    /// 移除顺序帧命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifySequentialFrame))]
    private void RemoveSequentialFrame()
    {
        if (SelectedSequentialFrame != null)
        {
            SequentialFrames.Remove(SelectedSequentialFrame);
            UpdateSequentialFrameSortOrders();
            AutoSave();
        }
    }

    private bool CanModifySequentialFrame() => SelectedSequentialFrame != null;

    /// <summary>
    /// 上移顺序帧命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveSequentialFrameUp))]
    private void MoveSequentialFrameUp()
    {
        if (SelectedSequentialFrame == null) return;
        var index = SequentialFrames.IndexOf(SelectedSequentialFrame);
        if (index > 0)
        {
            SequentialFrames.Move(index, index - 1);
            UpdateSequentialFrameSortOrders();
            AutoSave();
        }
    }

    private bool CanMoveSequentialFrameUp() => SelectedSequentialFrame != null && SequentialFrames.IndexOf(SelectedSequentialFrame) > 0;

    /// <summary>
    /// 下移顺序帧命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveSequentialFrameDown))]
    private void MoveSequentialFrameDown()
    {
        if (SelectedSequentialFrame == null) return;
        var index = SequentialFrames.IndexOf(SelectedSequentialFrame);
        if (index >= 0 && index < SequentialFrames.Count - 1)
        {
            SequentialFrames.Move(index, index + 1);
            UpdateSequentialFrameSortOrders();
            AutoSave();
        }
    }

    private bool CanMoveSequentialFrameDown() => SelectedSequentialFrame != null && SequentialFrames.IndexOf(SelectedSequentialFrame) < SequentialFrames.Count - 1;

    /// <summary>
    /// 清空日志命令
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        ReplyLogs.Clear();
    }

    #endregion

    public AutoReplyViewModel(IAutoReplyService autoReplyService, IConfigurationService configurationService)
    {
        _autoReplyService = autoReplyService;
        _configurationService = configurationService;

        // 订阅事件
        _autoReplyService.ReplyTriggered += OnReplyTriggered;

        // 加载配置
        LoadConfig();
    }

    /// <summary>
    /// 从配置加载数据
    /// </summary>
    private void LoadConfig()
    {
        var appConfig = _configurationService.Load();
        var config = appConfig.AutoReplyConfig;

        // IsRunning 不从配置恢复，始终从停止状态开始
        IsRunning = false;
        GlobalDelayMs = config.GlobalDelayMs;
        ActiveMode = config.ActiveMode;
        EnableLoop = config.SequentialConfig.EnableLoop;
        CurrentFrameIndex = config.SequentialConfig.CurrentIndex;

        // 加载匹配规则
        MatchRules.Clear();
        foreach (var rule in config.MatchConfig.Rules.OrderBy(r => r.SortOrder))
        {
            MatchRules.Add(MatchRuleViewModel.FromModel(rule));
        }

        // 加载顺序帧
        SequentialFrames.Clear();
        foreach (var frame in config.SequentialConfig.Frames.OrderBy(f => f.SortOrder))
        {
            SequentialFrames.Add(SequentialFrameViewModel.FromModel(frame));
        }

        // 同步到服务
        _autoReplyService.UpdateConfig(config);
    }

    /// <summary>
    /// 同步 ViewModel 数据到服务
    /// </summary>
    private void SyncToService()
    {
        var config = new AutoReplyConfig
        {
            IsEnabled = IsRunning, // 使用 IsRunning 作为启用状态
            GlobalDelayMs = GlobalDelayMs,
            ActiveMode = ActiveMode,
            MatchConfig = new MatchReplyConfig
            {
                Rules = MatchRules.Select(r => r.ToModel()).ToList()
            },
            SequentialConfig = new SequentialReplyConfig
            {
                Frames = SequentialFrames.Select(f => f.ToModel()).ToList(),
                EnableLoop = EnableLoop,
                CurrentIndex = CurrentFrameIndex
            }
        };

        _autoReplyService.UpdateConfig(config);
    }

    /// <summary>
    /// 自动保存配置
    /// </summary>
    private void AutoSave()
    {
        SyncToService();
        var appConfig = _configurationService.Load();
        appConfig.AutoReplyConfig = _autoReplyService.Config;
        _configurationService.Save(appConfig);
    }

    /// <summary>
    /// 处理回复触发事件
    /// </summary>
    private void OnReplyTriggered(object? sender, ReplyEventArgs e)
    {
        // 更新计数
        ReceiveCount = _autoReplyService.ReceiveCount;
        ReplyCount = _autoReplyService.ReplyCount;
        CurrentFrameIndex = _autoReplyService.Config.SequentialConfig.CurrentIndex;

        // 添加日志
        var logEntry = new ReplyLogEntry
        {
            Timestamp = e.Timestamp,
            RuleName = e.RuleName ?? "Unknown",
            ReceivedDataHex = BitConverter.ToString(e.ReceivedData).Replace("-", " "),
            ReplyDataHex = BitConverter.ToString(e.ReplyData).Replace("-", " ")
        };

        // 确保在 UI 线程上操作
        ReplyLogs.Insert(0, logEntry);

        // 限制日志数量
        while (ReplyLogs.Count > 100)
        {
            ReplyLogs.RemoveAt(ReplyLogs.Count - 1);
        }
    }

    private int GetNextMatchRuleId()
    {
        return MatchRules.Count > 0 ? MatchRules.Max(r => r.Id) + 1 : 1;
    }

    private int GetNextSequentialFrameId()
    {
        return SequentialFrames.Count > 0 ? SequentialFrames.Max(f => f.Id) + 1 : 1;
    }

    private void UpdateMatchRuleSortOrders()
    {
        for (int i = 0; i < MatchRules.Count; i++)
        {
            MatchRules[i].SortOrder = i;
        }
    }

    private void UpdateSequentialFrameSortOrders()
    {
        for (int i = 0; i < SequentialFrames.Count; i++)
        {
            SequentialFrames[i].SortOrder = i;
        }
    }

    /// <summary>
    /// 当属性变化时自动保存
    /// </summary>
    partial void OnGlobalDelayMsChanged(int value) => AutoSave();
    partial void OnActiveModeChanged(ReplyMode value) => AutoSave();
    partial void OnEnableLoopChanged(bool value) => AutoSave();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _autoReplyService.ReplyTriggered -= OnReplyTriggered;
        }

        _disposed = true;
    }
}

/// <summary>
/// 匹配规则 ViewModel
/// </summary>
public partial class MatchRuleViewModel : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _triggerPattern = string.Empty;
    [ObservableProperty] private Models.MatchType _matchType = Models.MatchType.HexContains;
    [ObservableProperty] private string _responseContent = string.Empty;
    [ObservableProperty] private bool _isResponseHex = true;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private string _description = string.Empty;

    public MatchRule ToModel() => new()
    {
        Id = Id,
        Name = Name,
        TriggerPattern = TriggerPattern,
        MatchType = MatchType,
        ResponseContent = ResponseContent,
        IsResponseHex = IsResponseHex,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        Description = Description
    };

    public static MatchRuleViewModel FromModel(MatchRule model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        TriggerPattern = model.TriggerPattern,
        MatchType = model.MatchType,
        ResponseContent = model.ResponseContent,
        IsResponseHex = model.IsResponseHex,
        IsEnabled = model.IsEnabled,
        SortOrder = model.SortOrder,
        Description = model.Description
    };
}

/// <summary>
/// 顺序帧 ViewModel
/// </summary>
public partial class SequentialFrameViewModel : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private bool _isHexMode = true;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private string _description = string.Empty;

    public SequentialFrame ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Content = Content,
        IsHexMode = IsHexMode,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        Description = Description
    };

    public static SequentialFrameViewModel FromModel(SequentialFrame model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Content = model.Content,
        IsHexMode = model.IsHexMode,
        IsEnabled = model.IsEnabled,
        SortOrder = model.SortOrder,
        Description = model.Description
    };
}

/// <summary>
/// 回复日志条目
/// </summary>
public class ReplyLogEntry
{
    public DateTime Timestamp { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public string ReceivedDataHex { get; init; } = string.Empty;
    public string ReplyDataHex { get; init; } = string.Empty;

    public string TimestampString => Timestamp.ToString("HH:mm:ss.fff");
}
