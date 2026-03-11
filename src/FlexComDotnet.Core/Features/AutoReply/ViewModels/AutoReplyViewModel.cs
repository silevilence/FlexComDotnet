using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.AutoReply.ViewModels;

/// <summary>
/// 自动回复功能 ViewModel - 统一规则池架构
/// </summary>
public partial class AutoReplyViewModel : ObservableObject, IDisposable
{
    private readonly IAutoReplyService _autoReplyService;
    private readonly IConfigurationService _configurationService;
    private readonly ILoggingService? _loggingService;
    private readonly IProtocolParserService? _protocolParserService;
    private readonly SynchronizationContext? _syncContext;
    private bool _disposed;
    private bool _isLoading;

    #region Observable Properties

    /// <summary>
    /// 全局回复延迟（毫秒）
    /// </summary>
    [ObservableProperty]
    private int _globalDelayMs = 100;

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
    /// 统一规则池列表
    /// </summary>
    public ObservableCollection<AutoReplyRuleViewModel> Rules { get; } = [];

    /// <summary>
    /// 当前选中的规则
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveRuleUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveRuleDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetRuleStateCommand))]
    private AutoReplyRuleViewModel? _selectedRule;

    /// <summary>
    /// 回复日志
    /// </summary>
    public ObservableCollection<ReplyLogEntry> ReplyLogs { get; } = [];

    /// <summary>
    /// 可用的协议定义列表
    /// </summary>
    public ObservableCollection<FrameDefinition> AvailableProtocols { get; } = [];

    /// <summary>
    /// 当前编辑的协议字段输入项（协议回复规则专用）
    /// </summary>
    public ObservableCollection<FieldInputItem> ProtocolFieldInputs { get; } = [];

    /// <summary>
    /// 当前编辑的顺序帧列表（顺序回复规则专用）
    /// </summary>
    public ObservableCollection<SequentialFrameViewModel> EditingFrames { get; } = [];

    /// <summary>
    /// 当前编辑中选中的顺序帧
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFrameCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveFrameUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveFrameDownCommand))]
    private SequentialFrameViewModel? _selectedEditingFrame;

    /// <summary>
    /// 规则编辑前的备份（用于取消编辑）
    /// </summary>
    private AutoReplyRule? _ruleBackup;

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
    /// 重置指定规则的处理器状态（如顺序回复索引）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyRule))]
    private void ResetRuleState()
    {
        if (SelectedRule == null) return;
        _autoReplyService.ResetRuleState(SelectedRule.Id);
        if (SelectedRule.Type == ReplyMode.Sequential)
        {
            SelectedRule.CurrentFrameIndex = 0;
        }
    }

    #region Rule CRUD

    /// <summary>
    /// 添加匹配回复规则
    /// </summary>
    [RelayCommand]
    private void AddMatchRule()
    {
        var rule = new AutoReplyRuleViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"匹配规则 {Rules.Count(r => r.Type == ReplyMode.Match) + 1}",
            Type = ReplyMode.Match,
            SortOrder = Rules.Count,
            IsEnabled = true,
            MatchConfig = new MatchRuleConfig(),
            IsEnabledChangedCallback = OnRuleIsEnabledChanged
        };
        Rules.Add(rule);
        SelectedRule = rule;
        AutoSave();
    }

    /// <summary>
    /// 添加顺序回复规则
    /// </summary>
    [RelayCommand]
    private void AddSequentialRule()
    {
        var rule = new AutoReplyRuleViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"顺序规则 {Rules.Count(r => r.Type == ReplyMode.Sequential) + 1}",
            Type = ReplyMode.Sequential,
            SortOrder = Rules.Count,
            IsEnabled = true,
            SequentialConfig = new SequentialRuleConfig(),
            IsEnabledChangedCallback = OnRuleIsEnabledChanged
        };
        Rules.Add(rule);
        SelectedRule = rule;
        AutoSave();
    }

    /// <summary>
    /// 添加协议回复规则
    /// </summary>
    [RelayCommand]
    private void AddProtocolRule()
    {
        var rule = new AutoReplyRuleViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"协议规则 {Rules.Count(r => r.Type == ReplyMode.Protocol) + 1}",
            Type = ReplyMode.Protocol,
            SortOrder = Rules.Count,
            IsEnabled = true,
            ProtocolConfig = new ProtocolRuleConfig(),
            IsEnabledChangedCallback = OnRuleIsEnabledChanged
        };
        Rules.Add(rule);
        SelectedRule = rule;
        RefreshAvailableProtocols();
        AutoSave();
    }

    /// <summary>
    /// 移除规则命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyRule))]
    private void RemoveRule()
    {
        if (SelectedRule != null)
        {
            Rules.Remove(SelectedRule);
            UpdateRuleSortOrders();
            AutoSave();
        }
    }

    private bool CanModifyRule() => SelectedRule != null;

    /// <summary>
    /// 保存规则编辑
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyRule))]
    private void SaveRule()
    {
        if (SelectedRule == null) return;

        // 对于顺序回复规则，同步编辑中的帧列表
        if (SelectedRule.Type == ReplyMode.Sequential && SelectedRule.SequentialConfig != null)
        {
            SelectedRule.SequentialConfig.Frames = EditingFrames.Select(f => f.ToModel()).ToList();
        }

        // 对于协议回复规则，同步字段值
        if (SelectedRule.Type == ReplyMode.Protocol && SelectedRule.ProtocolConfig != null)
        {
            SelectedRule.ProtocolConfig.FieldValues.Clear();
            foreach (var input in ProtocolFieldInputs)
            {
                if (!string.IsNullOrEmpty(input.Value))
                {
                    SelectedRule.ProtocolConfig.FieldValues[input.FieldName] = input.Value;
                }
            }
        }

        _ruleBackup = null;
        AutoSave();
        SelectedRule = null;
    }

    /// <summary>
    /// 取消规则编辑，恢复到编辑前状态
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyRule))]
    private void CancelRule()
    {
        if (SelectedRule != null && _ruleBackup != null)
        {
            SelectedRule.Name = _ruleBackup.Name;
            SelectedRule.Description = _ruleBackup.Description;
            SelectedRule.IsEnabled = _ruleBackup.IsEnabled;

            if (_ruleBackup.MatchConfig != null)
            {
                SelectedRule.MatchConfig = new MatchRuleConfig
                {
                    TriggerPattern = _ruleBackup.MatchConfig.TriggerPattern,
                    MatchType = _ruleBackup.MatchConfig.MatchType,
                    ResponseContent = _ruleBackup.MatchConfig.ResponseContent,
                    IsResponseHex = _ruleBackup.MatchConfig.IsResponseHex
                };
            }

            if (_ruleBackup.SequentialConfig != null)
            {
                SelectedRule.SequentialConfig = new SequentialRuleConfig
                {
                    Frames = _ruleBackup.SequentialConfig.Frames.Select(f => new SequentialFrame
                    {
                        Id = f.Id, Name = f.Name, Content = f.Content,
                        IsHexMode = f.IsHexMode, IsEnabled = f.IsEnabled, SortOrder = f.SortOrder,
                        Description = f.Description
                    }).ToList(),
                    EnableLoop = _ruleBackup.SequentialConfig.EnableLoop,
                    CurrentIndex = _ruleBackup.SequentialConfig.CurrentIndex
                };
            }

            if (_ruleBackup.ProtocolConfig != null)
            {
                SelectedRule.ProtocolConfig = new ProtocolRuleConfig
                {
                    ProtocolName = _ruleBackup.ProtocolConfig.ProtocolName,
                    FieldValues = new Dictionary<string, string>(_ruleBackup.ProtocolConfig.FieldValues)
                };
            }
            _ruleBackup = null;
        }
        SelectedRule = null;
    }

    /// <summary>
    /// 上移规则命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveRuleUp))]
    private void MoveRuleUp()
    {
        if (SelectedRule == null) return;
        var index = Rules.IndexOf(SelectedRule);
        if (index > 0)
        {
            Rules.Move(index, index - 1);
            UpdateRuleSortOrders();
            AutoSave();
        }
    }

    private bool CanMoveRuleUp() => SelectedRule != null && Rules.IndexOf(SelectedRule) > 0;

    /// <summary>
    /// 下移规则命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveRuleDown))]
    private void MoveRuleDown()
    {
        if (SelectedRule == null) return;
        var index = Rules.IndexOf(SelectedRule);
        if (index >= 0 && index < Rules.Count - 1)
        {
            Rules.Move(index, index + 1);
            UpdateRuleSortOrders();
            AutoSave();
        }
    }

    private bool CanMoveRuleDown() => SelectedRule != null && Rules.IndexOf(SelectedRule) < Rules.Count - 1;

    #endregion

    #region Sequential Frame Management (editing context)

    /// <summary>
    /// 添加顺序帧到当前编辑的规则
    /// </summary>
    [RelayCommand]
    private void AddFrame()
    {
        var frame = new SequentialFrameViewModel
        {
            Id = EditingFrames.Count > 0 ? EditingFrames.Max(f => f.Id) + 1 : 1,
            Name = $"帧 {EditingFrames.Count + 1}",
            SortOrder = EditingFrames.Count,
            IsEnabled = true
        };
        EditingFrames.Add(frame);
        SelectedEditingFrame = frame;
    }

    /// <summary>
    /// 移除顺序帧
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyFrame))]
    private void RemoveFrame()
    {
        if (SelectedEditingFrame != null)
        {
            EditingFrames.Remove(SelectedEditingFrame);
            UpdateFrameSortOrders();
        }
    }

    private bool CanModifyFrame() => SelectedEditingFrame != null;

    /// <summary>
    /// 上移顺序帧
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveFrameUp))]
    private void MoveFrameUp()
    {
        if (SelectedEditingFrame == null) return;
        var index = EditingFrames.IndexOf(SelectedEditingFrame);
        if (index > 0)
        {
            EditingFrames.Move(index, index - 1);
            UpdateFrameSortOrders();
        }
    }

    private bool CanMoveFrameUp() => SelectedEditingFrame != null && EditingFrames.IndexOf(SelectedEditingFrame) > 0;

    /// <summary>
    /// 下移顺序帧
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveFrameDown))]
    private void MoveFrameDown()
    {
        if (SelectedEditingFrame == null) return;
        var index = EditingFrames.IndexOf(SelectedEditingFrame);
        if (index >= 0 && index < EditingFrames.Count - 1)
        {
            EditingFrames.Move(index, index + 1);
            UpdateFrameSortOrders();
        }
    }

    private bool CanMoveFrameDown() => SelectedEditingFrame != null && EditingFrames.IndexOf(SelectedEditingFrame) < EditingFrames.Count - 1;

    #endregion

    #region Protocol helpers

    /// <summary>
    /// 刷新可用协议列表
    /// </summary>
    [RelayCommand]
    private void RefreshAvailableProtocols()
    {
        AvailableProtocols.Clear();
        if (_protocolParserService == null) return;
        foreach (var def in _protocolParserService.GetAllDefinitions())
        {
            AvailableProtocols.Add(def);
        }
    }

    /// <summary>
    /// 当选中规则的协议变化时，刷新字段输入
    /// </summary>
    [RelayCommand]
    private void RefreshProtocolFields()
    {
        ProtocolFieldInputs.Clear();
        if (SelectedRule?.ProtocolConfig == null || _protocolParserService == null)
            return;

        var protocolName = SelectedRule.ProtocolConfig.ProtocolName;
        if (string.IsNullOrEmpty(protocolName))
            return;

        var definitions = _protocolParserService.GetAllDefinitions();
        var definition = definitions.FirstOrDefault(d => d.Name == protocolName);
        if (definition == null) return;

        // DL/T 645 固定字段
        if (definition.ProtocolType == Protocol.Models.ProtocolType.Dlt645)
        {
            ProtocolFieldInputs.Add(new FieldInputItem
            {
                FieldName = "电表地址",
                DisplayName = "电表地址",
                Description = "12位BCD码地址",
                DataType = Protocol.Models.DataType.AsciiString,
                DefaultValue = "000000000000"
            });
            ProtocolFieldInputs.Add(new FieldInputItem
            {
                FieldName = "控制码",
                DisplayName = "控制码",
                Description = "功能控制字节 (Hex)",
                DataType = Protocol.Models.DataType.UInt8,
                DefaultValue = "11"
            });
            ProtocolFieldInputs.Add(new FieldInputItem
            {
                FieldName = "数据标识",
                DisplayName = "数据标识",
                Description = "4字节数据标识 (十进制)",
                DataType = Protocol.Models.DataType.UInt32,
                DefaultValue = "65536"
            });
        }

        // 用户定义字段
        foreach (var field in definition.Fields.Where(f => f.IsEnabled))
        {
            ProtocolFieldInputs.Add(new FieldInputItem
            {
                FieldName = field.Name,
                DisplayName = field.Name,
                Description = field.Description,
                DataType = field.DataType,
                DefaultValue = string.Empty,
                IsHexMode = field.DataType is Protocol.Models.DataType.Bytes or Protocol.Models.DataType.UInt8
            });
        }

        // 恢复已保存的值
        if (SelectedRule.ProtocolConfig.FieldValues.Count > 0)
        {
            foreach (var input in ProtocolFieldInputs)
            {
                if (SelectedRule.ProtocolConfig.FieldValues.TryGetValue(input.FieldName, out var savedValue))
                {
                    input.Value = savedValue;
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// 清空日志命令
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        ReplyLogs.Clear();
    }

    #endregion

    public AutoReplyViewModel(IAutoReplyService autoReplyService, IConfigurationService configurationService, ILoggingService? loggingService = null, IProtocolParserService? protocolParserService = null)
    {
        _autoReplyService = autoReplyService;
        _configurationService = configurationService;
        _loggingService = loggingService;
        _protocolParserService = protocolParserService;

        // 捕获 UI 线程的同步上下文，用于跨线程更新 UI
        _syncContext = SynchronizationContext.Current;

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
        _isLoading = true;
        try
        {
            var appConfig = _configurationService.Load();
            var config = appConfig.AutoReplyConfig;

            // IsRunning 不从配置恢复，始终从停止状态开始
            IsRunning = false;
            GlobalDelayMs = config.GlobalDelayMs;

            // 加载统一规则池
            Rules.Clear();
            foreach (var rule in config.Rules.OrderBy(r => r.SortOrder))
            {
                var ruleVm = AutoReplyRuleViewModel.FromModel(rule);
                ruleVm.IsEnabledChangedCallback = OnRuleIsEnabledChanged;
                Rules.Add(ruleVm);
            }

            // 刷新可用协议列表
            RefreshAvailableProtocols();

            // 同步到服务
            _autoReplyService.UpdateConfig(config);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// 同步 ViewModel 数据到服务
    /// </summary>
    private void SyncToService()
    {
        var config = new AutoReplyConfig
        {
            IsEnabled = IsRunning,
            GlobalDelayMs = GlobalDelayMs,
            Rules = Rules.Select(r => r.ToModel()).ToList()
        };

        _autoReplyService.UpdateConfig(config);
    }

    /// <summary>
    /// 规则启用状态变更回调（运行中立即同步到服务）
    /// </summary>
    private void OnRuleIsEnabledChanged()
    {
        if (!_isLoading)
        {
            AutoSave();
        }
    }

    /// <summary>
    /// 自动保存配置
    /// </summary>
    private void AutoSave()
    {
        if (_isLoading) return;

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
        var logEntry = new ReplyLogEntry
        {
            Timestamp = e.Timestamp,
            RuleName = e.RuleName ?? "Unknown",
            ReceivedDataHex = BitConverter.ToString(e.ReceivedData).Replace("-", " "),
            ReplyDataHex = BitConverter.ToString(e.ReplyData).Replace("-", " ")
        };

        _loggingService?.Info(LogSource.AutoReply,
            $"触发回复 [{logEntry.RuleName}] Rx: {logEntry.ReceivedDataHex} → Tx: {logEntry.ReplyDataHex}");

        if (_syncContext != null)
        {
            _syncContext.Post(_ => UpdateUIOnReply(logEntry), null);
        }
        else
        {
            UpdateUIOnReply(logEntry);
        }
    }

    /// <summary>
    /// 在 UI 线程上更新计数和日志
    /// </summary>
    private void UpdateUIOnReply(ReplyLogEntry logEntry)
    {
        ReceiveCount = _autoReplyService.ReceiveCount;
        ReplyCount = _autoReplyService.ReplyCount;

        // 添加日志
        ReplyLogs.Insert(0, logEntry);

        // 限制日志数量
        while (ReplyLogs.Count > 100)
        {
            ReplyLogs.RemoveAt(ReplyLogs.Count - 1);
        }
    }

    private void UpdateRuleSortOrders()
    {
        for (int i = 0; i < Rules.Count; i++)
        {
            Rules[i].SortOrder = i;
        }
    }

    private void UpdateFrameSortOrders()
    {
        for (int i = 0; i < EditingFrames.Count; i++)
        {
            EditingFrames[i].SortOrder = i;
        }
    }

    /// <summary>
    /// 当属性变化时自动保存
    /// </summary>
    partial void OnGlobalDelayMsChanged(int value) => AutoSave();

    /// <summary>
    /// 选中规则变更时备份数据并加载编辑上下文
    /// </summary>
    partial void OnSelectedRuleChanged(AutoReplyRuleViewModel? value)
    {
        _ruleBackup = value?.ToModel();

        // 加载顺序帧编辑上下文
        EditingFrames.Clear();
        SelectedEditingFrame = null;
        if (value?.Type == ReplyMode.Sequential && value.SequentialConfig != null)
        {
            foreach (var frame in value.SequentialConfig.Frames.OrderBy(f => f.SortOrder))
            {
                EditingFrames.Add(SequentialFrameViewModel.FromModel(frame));
            }
        }

        // 加载协议字段编辑上下文
        ProtocolFieldInputs.Clear();
        if (value?.Type == ReplyMode.Protocol)
        {
            RefreshAvailableProtocols();
            RefreshProtocolFields();
        }
    }

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
/// 统一规则 ViewModel
/// </summary>
public partial class AutoReplyRuleViewModel : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ReplyMode _type = ReplyMode.Match;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private int _currentFrameIndex;

    /// <summary>
    /// 启用状态变更回调（供父 ViewModel 订阅）
    /// </summary>
    public Action? IsEnabledChangedCallback { get; set; }

    partial void OnIsEnabledChanged(bool value) => IsEnabledChangedCallback?.Invoke();

    /// <summary>
    /// 匹配回复配置
    /// </summary>
    [ObservableProperty]
    private MatchRuleConfig? _matchConfig;

    /// <summary>
    /// 顺序回复配置
    /// </summary>
    [ObservableProperty]
    private SequentialRuleConfig? _sequentialConfig;

    /// <summary>
    /// 协议回复配置
    /// </summary>
    [ObservableProperty]
    private ProtocolRuleConfig? _protocolConfig;

    /// <summary>
    /// 类型显示名称
    /// </summary>
    public string TypeDisplayName => Type switch
    {
        ReplyMode.Match => "匹配",
        ReplyMode.Sequential => "顺序",
        ReplyMode.Protocol => "协议",
        ReplyMode.Script => "脚本",
        _ => "未知"
    };

    public AutoReplyRule ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Type = Type,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        MatchConfig = MatchConfig != null ? new MatchRuleConfig
        {
            TriggerPattern = MatchConfig.TriggerPattern,
            MatchType = MatchConfig.MatchType,
            ResponseContent = MatchConfig.ResponseContent,
            IsResponseHex = MatchConfig.IsResponseHex
        } : null,
        SequentialConfig = SequentialConfig != null ? new SequentialRuleConfig
        {
            Frames = SequentialConfig.Frames.Select(f => new SequentialFrame
            {
                Id = f.Id, Name = f.Name, Content = f.Content,
                IsHexMode = f.IsHexMode, IsEnabled = f.IsEnabled,
                SortOrder = f.SortOrder, Description = f.Description
            }).ToList(),
            EnableLoop = SequentialConfig.EnableLoop,
            CurrentIndex = SequentialConfig.CurrentIndex
        } : null,
        ProtocolConfig = ProtocolConfig != null ? new ProtocolRuleConfig
        {
            ProtocolName = ProtocolConfig.ProtocolName,
            FieldValues = new Dictionary<string, string>(ProtocolConfig.FieldValues)
        } : null
    };

    public static AutoReplyRuleViewModel FromModel(AutoReplyRule model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        Type = model.Type,
        IsEnabled = model.IsEnabled,
        SortOrder = model.SortOrder,
        MatchConfig = model.MatchConfig != null ? new MatchRuleConfig
        {
            TriggerPattern = model.MatchConfig.TriggerPattern,
            MatchType = model.MatchConfig.MatchType,
            ResponseContent = model.MatchConfig.ResponseContent,
            IsResponseHex = model.MatchConfig.IsResponseHex
        } : null,
        SequentialConfig = model.SequentialConfig != null ? new SequentialRuleConfig
        {
            Frames = model.SequentialConfig.Frames.Select(f => new SequentialFrame
            {
                Id = f.Id, Name = f.Name, Content = f.Content,
                IsHexMode = f.IsHexMode, IsEnabled = f.IsEnabled,
                SortOrder = f.SortOrder, Description = f.Description
            }).ToList(),
            EnableLoop = model.SequentialConfig.EnableLoop,
            CurrentIndex = model.SequentialConfig.CurrentIndex
        } : null,
        ProtocolConfig = model.ProtocolConfig != null ? new ProtocolRuleConfig
        {
            ProtocolName = model.ProtocolConfig.ProtocolName,
            FieldValues = new Dictionary<string, string>(model.ProtocolConfig.FieldValues)
        } : null,
        CurrentFrameIndex = model.SequentialConfig?.CurrentIndex ?? 0
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
