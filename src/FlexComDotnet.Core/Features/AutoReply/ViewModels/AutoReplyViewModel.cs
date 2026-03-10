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
/// 自动回复功能 ViewModel
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
    [NotifyCanExecuteChangedFor(nameof(SaveMatchRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelMatchRuleCommand))]
    private MatchRuleViewModel? _selectedMatchRule;

    /// <summary>
    /// 当前选中的顺序帧
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSequentialFrameCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSequentialFrameUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSequentialFrameDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSequentialFrameCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelSequentialFrameCommand))]
    private SequentialFrameViewModel? _selectedSequentialFrame;

    /// <summary>
    /// 匹配规则编辑前的备份（用于取消编辑）
    /// </summary>
    private MatchRule? _matchRuleBackup;

    /// <summary>
    /// 顺序帧编辑前的备份（用于取消编辑）
    /// </summary>
    private SequentialFrame? _sequentialFrameBackup;

    /// <summary>
    /// 协议回复方案编辑前的备份（用于取消编辑）
    /// </summary>
    private ProtocolReplyScheme? _protocolSchemeBackup;

    /// <summary>
    /// 协议回复方案列表
    /// </summary>
    public ObservableCollection<ProtocolReplySchemeViewModel> ProtocolSchemes { get; } = [];

    /// <summary>
    /// 当前选中的协议回复方案
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveProtocolSchemeCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveProtocolSchemeFieldsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelProtocolSchemeCommand))]
    private ProtocolReplySchemeViewModel? _selectedProtocolScheme;

    /// <summary>
    /// 可用的协议定义列表
    /// </summary>
    public ObservableCollection<FrameDefinition> AvailableProtocols { get; } = [];

    /// <summary>
    /// 协议回复方案的字段输入项
    /// </summary>
    public ObservableCollection<FieldInputItem> ProtocolFieldInputs { get; } = [];

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
    /// 保存匹配规则编辑
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyMatchRule))]
    private void SaveMatchRule()
    {
        _matchRuleBackup = null;
        AutoSave();
        SelectedMatchRule = null;
    }

    /// <summary>
    /// 取消匹配规则编辑，恢复到编辑前状态
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyMatchRule))]
    private void CancelMatchRule()
    {
        if (SelectedMatchRule != null && _matchRuleBackup != null)
        {
            SelectedMatchRule.Name = _matchRuleBackup.Name;
            SelectedMatchRule.TriggerPattern = _matchRuleBackup.TriggerPattern;
            SelectedMatchRule.MatchType = _matchRuleBackup.MatchType;
            SelectedMatchRule.ResponseContent = _matchRuleBackup.ResponseContent;
            SelectedMatchRule.IsResponseHex = _matchRuleBackup.IsResponseHex;
            SelectedMatchRule.Description = _matchRuleBackup.Description;
            _matchRuleBackup = null;
        }
        SelectedMatchRule = null;
    }

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
    /// 保存顺序帧编辑
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifySequentialFrame))]
    private void SaveSequentialFrame()
    {
        _sequentialFrameBackup = null;
        AutoSave();
        SelectedSequentialFrame = null;
    }

    /// <summary>
    /// 取消顺序帧编辑，恢复到编辑前状态
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifySequentialFrame))]
    private void CancelSequentialFrame()
    {
        if (SelectedSequentialFrame != null && _sequentialFrameBackup != null)
        {
            SelectedSequentialFrame.Name = _sequentialFrameBackup.Name;
            SelectedSequentialFrame.Content = _sequentialFrameBackup.Content;
            SelectedSequentialFrame.IsHexMode = _sequentialFrameBackup.IsHexMode;
            SelectedSequentialFrame.Description = _sequentialFrameBackup.Description;
            _sequentialFrameBackup = null;
        }
        SelectedSequentialFrame = null;
    }

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

    #region Protocol Reply Commands

    /// <summary>
    /// 添加协议回复方案
    /// </summary>
    [RelayCommand]
    private void AddProtocolScheme()
    {
        var scheme = new ProtocolReplySchemeViewModel
        {
            Id = ProtocolSchemes.Count > 0 ? ProtocolSchemes.Max(s => s.Id) + 1 : 1,
            Name = $"方案 {ProtocolSchemes.Count + 1}",
            SortOrder = ProtocolSchemes.Count,
            IsEnabled = true
        };
        ProtocolSchemes.Add(scheme);
        SelectedProtocolScheme = scheme;
        AutoSave();
    }

    /// <summary>
    /// 移除协议回复方案
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyProtocolScheme))]
    private void RemoveProtocolScheme()
    {
        if (SelectedProtocolScheme == null) return;
        ProtocolSchemes.Remove(SelectedProtocolScheme);
        for (int i = 0; i < ProtocolSchemes.Count; i++)
            ProtocolSchemes[i].SortOrder = i;
        AutoSave();
    }

    private bool CanModifyProtocolScheme() => SelectedProtocolScheme != null;

    /// <summary>
    /// 设置激活的协议回复方案（单选互斥）
    /// </summary>
    [RelayCommand]
    private void SetActiveProtocolScheme(ProtocolReplySchemeViewModel? scheme)
    {
        foreach (var s in ProtocolSchemes)
        {
            s.IsActive = s == scheme;
        }
        AutoSave();
    }

    /// <summary>
    /// 刷新可用协议列表
    /// </summary>
    [RelayCommand]
    private void RefreshAvailableProtocols()
    {
        // 保存当前正在编辑的协议名，防止刷新列表时清空选择
        var currentProtocolName = SelectedProtocolScheme?.ProtocolName;

        AvailableProtocols.Clear();
        if (_protocolParserService == null) return;
        foreach (var def in _protocolParserService.GetAllDefinitions())
        {
            AvailableProtocols.Add(def);
        }

        // 恢复编辑中的协议选择
        if (SelectedProtocolScheme != null && !string.IsNullOrEmpty(currentProtocolName))
        {
            SelectedProtocolScheme.ProtocolName = currentProtocolName;
        }
    }

    /// <summary>
    /// 当选中方案的协议变化时，刷新字段输入
    /// </summary>
    [RelayCommand]
    private void RefreshProtocolFields()
    {
        ProtocolFieldInputs.Clear();
        if (SelectedProtocolScheme == null || _protocolParserService == null)
            return;

        var protocolName = SelectedProtocolScheme.ProtocolName;
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
        if (SelectedProtocolScheme.FieldValues.Count > 0)
        {
            foreach (var input in ProtocolFieldInputs)
            {
                if (SelectedProtocolScheme.FieldValues.TryGetValue(input.FieldName, out var savedValue))
                {
                    input.Value = savedValue;
                }
            }
        }
    }

    /// <summary>
    /// 保存当前方案的字段值
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyProtocolScheme))]
    private void SaveProtocolSchemeFields()
    {
        if (SelectedProtocolScheme == null) return;

        SelectedProtocolScheme.FieldValues.Clear();
        foreach (var input in ProtocolFieldInputs)
        {
            if (!string.IsNullOrEmpty(input.Value))
            {
                SelectedProtocolScheme.FieldValues[input.FieldName] = input.Value;
            }
        }
        _protocolSchemeBackup = null;
        AutoSave();
        SelectedProtocolScheme = null;
    }

    /// <summary>
    /// 取消协议回复方案编辑，恢复到编辑前状态
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifyProtocolScheme))]
    private void CancelProtocolScheme()
    {
        if (SelectedProtocolScheme != null && _protocolSchemeBackup != null)
        {
            SelectedProtocolScheme.Name = _protocolSchemeBackup.Name;
            SelectedProtocolScheme.Description = _protocolSchemeBackup.Description;
            SelectedProtocolScheme.ProtocolName = _protocolSchemeBackup.ProtocolName;
            SelectedProtocolScheme.FieldValues = new Dictionary<string, string>(_protocolSchemeBackup.FieldValues);
            _protocolSchemeBackup = null;
        }
        SelectedProtocolScheme = null;
    }

    #endregion

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

        // 加载协议回复方案
        ProtocolSchemes.Clear();
        foreach (var scheme in config.ProtocolConfig.Schemes.OrderBy(s => s.SortOrder))
        {
            var vm = ProtocolReplySchemeViewModel.FromModel(scheme);
            vm.IsActive = config.ProtocolConfig.ActiveSchemeIndex >= 0
                && config.ProtocolConfig.Schemes.IndexOf(scheme) == config.ProtocolConfig.ActiveSchemeIndex;
            ProtocolSchemes.Add(vm);
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
        var protocolConfig = new ProtocolReplyConfig
        {
            Schemes = ProtocolSchemes.Select(s => s.ToModel()).ToList(),
            ActiveSchemeIndex = ProtocolSchemes.ToList().FindIndex(s => s.IsActive)
        };

        var config = new AutoReplyConfig
        {
            IsEnabled = IsRunning,
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
            },
            ProtocolConfig = protocolConfig
        };

        _autoReplyService.UpdateConfig(config);
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
        // 预先构建日志条目（可在后台线程执行）
        var logEntry = new ReplyLogEntry
        {
            Timestamp = e.Timestamp,
            RuleName = e.RuleName ?? "Unknown",
            ReceivedDataHex = BitConverter.ToString(e.ReceivedData).Replace("-", " "),
            ReplyDataHex = BitConverter.ToString(e.ReplyData).Replace("-", " ")
        };

        // 转发到统一日志服务
        _loggingService?.Info(LogSource.AutoReply,
            $"触发回复 [{logEntry.RuleName}] Rx: {logEntry.ReceivedDataHex} → Tx: {logEntry.ReplyDataHex}");

        // 确保在 UI 线程上操作 ObservableCollection 和属性更新
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
        // 更新计数
        ReceiveCount = _autoReplyService.ReceiveCount;
        ReplyCount = _autoReplyService.ReplyCount;
        CurrentFrameIndex = _autoReplyService.Config.SequentialConfig.CurrentIndex;

        // 添加日志
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

    /// <summary>
    /// 选中匹配规则变更时备份数据
    /// </summary>
    partial void OnSelectedMatchRuleChanged(MatchRuleViewModel? value)
    {
        _matchRuleBackup = value?.ToModel();
    }

    /// <summary>
    /// 选中顺序帧变更时备份数据
    /// </summary>
    partial void OnSelectedSequentialFrameChanged(SequentialFrameViewModel? value)
    {
        _sequentialFrameBackup = value?.ToModel();
    }

    /// <summary>
    /// 选中协议回复方案变更时刷新字段
    /// </summary>
    partial void OnSelectedProtocolSchemeChanged(ProtocolReplySchemeViewModel? value)
    {
        _protocolSchemeBackup = value?.ToModel();
        RefreshProtocolFields();
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

/// <summary>
/// 协议回复方案 ViewModel
/// </summary>
public partial class ProtocolReplySchemeViewModel : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _protocolName = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private bool _isActive;

    /// <summary>
    /// 字段值配置（字段名 -> 值表达式）
    /// </summary>
    public Dictionary<string, string> FieldValues { get; set; } = [];

    public ProtocolReplyScheme ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        ProtocolName = ProtocolName,
        FieldValues = new Dictionary<string, string>(FieldValues),
        IsEnabled = IsEnabled,
        SortOrder = SortOrder
    };

    public static ProtocolReplySchemeViewModel FromModel(ProtocolReplyScheme model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        ProtocolName = model.ProtocolName,
        FieldValues = new Dictionary<string, string>(model.FieldValues),
        IsEnabled = model.IsEnabled,
        SortOrder = model.SortOrder
    };
}
