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
    /// 当前顺序帧的协议字段输入项（协议组帧模式时使用）
    /// </summary>
    public ObservableCollection<FieldInputItem> SequentialFrameFieldInputs { get; } = [];

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

        // 匹配规则：同步断言和响应协议字段
        if (SelectedRule.Type == ReplyMode.Match && SelectedRule.MatchConfig != null)
        {
            SelectedRule.MatchConfig.FieldAssertions = EditingAssertions.Select(a => new FieldAssertion
            {
                FieldName = a.FieldName,
                Operator = a.Operator,
                ExpectedValue = a.ExpectedValue
            }).ToList();

            if (SelectedRule.MatchConfig.ResponseMode == ResponseBuildMode.ProtocolBuild)
            {
                SelectedRule.MatchConfig.ProtocolResponse.FieldValues.Clear();
                SelectedRule.MatchConfig.ProtocolResponse.FieldHexModes.Clear();
                foreach (var input in MatchResponseFieldInputs)
                {
                    if (!string.IsNullOrEmpty(input.Value))
                    {
                        SelectedRule.MatchConfig.ProtocolResponse.FieldValues[input.FieldName] = input.Value;
                        SelectedRule.MatchConfig.ProtocolResponse.FieldHexModes[input.FieldName] = input.IsHexMode;
                    }
                }
            }
        }

        // 对于顺序回复规则，同步编辑中的帧列表
        if (SelectedRule.Type == ReplyMode.Sequential && SelectedRule.SequentialConfig != null)
        {
            SyncSequentialFrameFieldsToModel();
            SelectedRule.SequentialConfig.Frames = EditingFrames.Select(f => f.ToModel()).ToList();
        }

        // 对于协议回复规则，同步字段值
        if (SelectedRule.Type == ReplyMode.Protocol && SelectedRule.ProtocolConfig != null)
        {
            SelectedRule.ProtocolConfig.FieldValues.Clear();
            SelectedRule.ProtocolConfig.FieldHexModes.Clear();
            foreach (var input in ProtocolFieldInputs)
            {
                if (!string.IsNullOrEmpty(input.Value))
                {
                    SelectedRule.ProtocolConfig.FieldValues[input.FieldName] = input.Value;
                    SelectedRule.ProtocolConfig.FieldHexModes[input.FieldName] = input.IsHexMode;
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
                    TriggerProtocolName = _ruleBackup.MatchConfig.TriggerProtocolName,
                    FieldAssertions = _ruleBackup.MatchConfig.FieldAssertions
                        .Select(a => new FieldAssertion
                        {
                            FieldName = a.FieldName,
                            Operator = a.Operator,
                            ExpectedValue = a.ExpectedValue
                        }).ToList(),
                    ResponseMode = _ruleBackup.MatchConfig.ResponseMode,
                    ResponseContent = _ruleBackup.MatchConfig.ResponseContent,
                    IsResponseHex = _ruleBackup.MatchConfig.IsResponseHex,
                    ProtocolResponse = new ProtocolResponseConfig
                    {
                        ProtocolName = _ruleBackup.MatchConfig.ProtocolResponse?.ProtocolName ?? string.Empty,
                        FieldValues = new Dictionary<string, string>(_ruleBackup.MatchConfig.ProtocolResponse?.FieldValues ?? []),
                        FieldHexModes = new Dictionary<string, bool>(_ruleBackup.MatchConfig.ProtocolResponse?.FieldHexModes ?? [])
                    }
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
                        Description = f.Description,
                        ResponseMode = f.ResponseMode,
                        ProtocolResponse = new ProtocolResponseConfig
                        {
                            ProtocolName = f.ProtocolResponse?.ProtocolName ?? string.Empty,
                            FieldValues = new Dictionary<string, string>(f.ProtocolResponse?.FieldValues ?? []),
                            FieldHexModes = new Dictionary<string, bool>(f.ProtocolResponse?.FieldHexModes ?? [])
                        }
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
                    FieldValues = new Dictionary<string, string>(_ruleBackup.ProtocolConfig.FieldValues),
                    FieldHexModes = new Dictionary<string, bool>(_ruleBackup.ProtocolConfig.FieldHexModes)
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
                IsHexMode = true
            });
        }

        // 恢复已保存的值和Hex模式
        if (SelectedRule.ProtocolConfig.FieldValues.Count > 0)
        {
            foreach (var input in ProtocolFieldInputs)
            {
                if (SelectedRule.ProtocolConfig.FieldValues.TryGetValue(input.FieldName, out var savedValue))
                {
                    input.Value = savedValue;
                }
                if (SelectedRule.ProtocolConfig.FieldHexModes.TryGetValue(input.FieldName, out var hexMode))
                {
                    input.IsHexMode = hexMode;
                }
            }
        }
    }

    #endregion

    #region Match Rule Assertion Management

    /// <summary>
    /// 当前匹配规则的字段断言列表（编辑用）
    /// </summary>
    public ObservableCollection<FieldAssertion> EditingAssertions { get; } = [];

    /// <summary>
    /// 当前触发协议的可用字段名称列表（用于断言字段名下拉选择）
    /// </summary>
    public ObservableCollection<string> AssertionFieldNames { get; } = [];

    /// <summary>
    /// 匹配规则的响应协议字段输入项
    /// </summary>
    public ObservableCollection<FieldInputItem> MatchResponseFieldInputs { get; } = [];

    /// <summary>
    /// 添加字段断言
    /// </summary>
    [RelayCommand]
    private void AddAssertion()
    {
        EditingAssertions.Add(new FieldAssertion());
    }

    /// <summary>
    /// 移除字段断言
    /// </summary>
    [RelayCommand]
    private void RemoveAssertion(FieldAssertion? assertion)
    {
        if (assertion != null)
        {
            EditingAssertions.Remove(assertion);
        }
    }

    /// <summary>
    /// 刷新匹配规则的响应协议字段
    /// </summary>
    [RelayCommand]
    private void RefreshMatchResponseFields()
    {
        MatchResponseFieldInputs.Clear();
        if (SelectedRule?.MatchConfig == null || _protocolParserService == null)
            return;

        var protocolName = SelectedRule.MatchConfig.ProtocolResponse.ProtocolName;
        if (string.IsNullOrEmpty(protocolName))
            return;

        var definitions = _protocolParserService.GetAllDefinitions();
        var definition = definitions.FirstOrDefault(d => d.Name == protocolName);
        if (definition == null) return;

        // 为 DL/T 645 协议添加固定字段
        AddDlt645FixedFields(definition, MatchResponseFieldInputs,
            SelectedRule.MatchConfig.ProtocolResponse.FieldValues,
            SelectedRule.MatchConfig.ProtocolResponse.FieldHexModes);

        foreach (var field in definition.Fields.Where(f => f.IsEnabled))
        {
            var input = new FieldInputItem
            {
                FieldName = field.Name,
                DisplayName = field.Name,
                Description = field.Description,
                DataType = field.DataType,
                DefaultValue = string.Empty,
                IsHexMode = true
            };

            // 恢复已保存的值和Hex模式
            if (SelectedRule.MatchConfig.ProtocolResponse.FieldValues.TryGetValue(field.Name, out var savedValue))
            {
                input.Value = savedValue;
            }
            if (SelectedRule.MatchConfig.ProtocolResponse.FieldHexModes.TryGetValue(field.Name, out var hexMode))
            {
                input.IsHexMode = hexMode;
            }

            MatchResponseFieldInputs.Add(input);
        }
    }

    /// <summary>
    /// 刷新断言可用字段名列表（基于触发协议）
    /// </summary>
    [RelayCommand]
    private void RefreshAssertionFieldNames()
    {
        AssertionFieldNames.Clear();
        if (SelectedRule?.MatchConfig == null || _protocolParserService == null)
            return;

        var protocolName = SelectedRule.MatchConfig.TriggerProtocolName;
        if (string.IsNullOrEmpty(protocolName)) return;

        var definition = _protocolParserService.GetAllDefinitions()
            .FirstOrDefault(d => d.Name == protocolName);
        if (definition == null) return;

        // DL/T 645 协议的固定字段（解析结果中包含但不在 definition.Fields 中）
        if (definition.ProtocolType == ProtocolType.Dlt645)
        {
            AssertionFieldNames.Add("电表地址");
            AssertionFieldNames.Add("控制码");
            AssertionFieldNames.Add("数据标识");
        }

        foreach (var field in definition.Fields)
        {
            AssertionFieldNames.Add(field.Name);
        }
    }

    #endregion

    #region Sequential Frame Protocol helpers

    /// <summary>
    /// 刷新顺序帧的协议字段输入项
    /// </summary>
    [RelayCommand]
    private void RefreshSequentialFrameFields()
    {
        SyncSequentialFrameFieldsToModel();
        SequentialFrameFieldInputs.Clear();
        if (SelectedEditingFrame == null || _protocolParserService == null)
            return;

        if (SelectedEditingFrame.ResponseMode != ResponseBuildMode.ProtocolBuild)
            return;

        var protocolName = SelectedEditingFrame.ProtocolName;
        if (string.IsNullOrEmpty(protocolName)) return;

        var definition = _protocolParserService.GetAllDefinitions()
            .FirstOrDefault(d => d.Name == protocolName);
        if (definition == null) return;

        // 为 DL/T 645 协议添加固定字段
        AddDlt645FixedFields(definition, SequentialFrameFieldInputs,
            SelectedEditingFrame.ProtocolFieldValues,
            SelectedEditingFrame.ProtocolFieldHexModes);

        foreach (var field in definition.Fields.Where(f => f.IsEnabled))
        {
            var input = new FieldInputItem
            {
                FieldName = field.Name,
                DisplayName = field.Name,
                Description = field.Description,
                DataType = field.DataType,
                DefaultValue = string.Empty,
                IsHexMode = true
            };

            if (SelectedEditingFrame.ProtocolFieldValues.TryGetValue(field.Name, out var savedValue))
            {
                input.Value = savedValue;
            }
            if (SelectedEditingFrame.ProtocolFieldHexModes.TryGetValue(field.Name, out var hexMode))
            {
                input.IsHexMode = hexMode;
            }

            SequentialFrameFieldInputs.Add(input);
        }
    }

    /// <summary>
    /// 将当前顺序帧协议字段输入同步回 SelectedEditingFrame
    /// </summary>
    private void SyncSequentialFrameFieldsToModel()
    {
        if (SelectedEditingFrame == null || SequentialFrameFieldInputs.Count == 0)
            return;

        SelectedEditingFrame.ProtocolFieldValues.Clear();
        SelectedEditingFrame.ProtocolFieldHexModes.Clear();
        foreach (var input in SequentialFrameFieldInputs)
        {
            if (!string.IsNullOrEmpty(input.Value))
            {
                SelectedEditingFrame.ProtocolFieldValues[input.FieldName] = input.Value;
                SelectedEditingFrame.ProtocolFieldHexModes[input.FieldName] = input.IsHexMode;
            }
        }
    }

    /// <summary>
    /// 为 DL/T 645 协议添加固定字段（电表地址、控制码、数据标识）
    /// </summary>
    private static void AddDlt645FixedFields(FrameDefinition definition,
        ObservableCollection<FieldInputItem> fieldInputs, Dictionary<string, string> savedValues,
        Dictionary<string, bool>? savedHexModes = null)
    {
        if (definition.ProtocolType != ProtocolType.Dlt645)
            return;

        var dlt645Fields = new (string Name, string Desc, DataType Type, string Default)[]
        {
            ("电表地址", "12位BCD码地址", DataType.AsciiString, "000000000000"),
            ("控制码", "功能控制字节 (Hex)", DataType.UInt8, "11"),
            ("数据标识", "4字节数据标识 (Hex)", DataType.UInt32, "00010000")
        };

        foreach (var (name, desc, dataType, defaultVal) in dlt645Fields)
        {
            var input = new FieldInputItem
            {
                FieldName = name,
                DisplayName = name,
                Description = desc,
                DataType = dataType,
                DefaultValue = defaultVal,
                IsHexMode = true
            };

            if (savedValues.TryGetValue(name, out var savedValue))
            {
                input.Value = savedValue;
            }
            if (savedHexModes?.TryGetValue(name, out var hexMode) == true)
            {
                input.IsHexMode = hexMode;
            }

            fieldInputs.Add(input);
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
    /// 选中顺序帧变更时同步协议字段
    /// </summary>
    partial void OnSelectedEditingFrameChanged(SequentialFrameViewModel? oldValue, SequentialFrameViewModel? newValue)
    {
        // 保存上一帧的协议字段
        if (oldValue != null)
        {
            SyncSequentialFrameFieldsToModel();
        }

        // 加载新帧的协议字段
        SequentialFrameFieldInputs.Clear();
        if (newValue?.ResponseMode == ResponseBuildMode.ProtocolBuild)
        {
            RefreshSequentialFrameFields();
        }
    }

    /// <summary>
    /// 选中规则变更时备份数据并加载编辑上下文
    /// </summary>
    partial void OnSelectedRuleChanged(AutoReplyRuleViewModel? value)
    {
        _ruleBackup = value?.ToModel();

        // 加载顺序帧编辑上下文
        EditingFrames.Clear();
        SequentialFrameFieldInputs.Clear();
        SelectedEditingFrame = null;
        if (value?.Type == ReplyMode.Sequential && value.SequentialConfig != null)
        {
            RefreshAvailableProtocols();
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

        // 加载匹配规则的断言和响应字段信息
        EditingAssertions.Clear();
        MatchResponseFieldInputs.Clear();
        AssertionFieldNames.Clear();
        if (value?.Type == ReplyMode.Match && value.MatchConfig != null)
        {
            RefreshAvailableProtocols();
            RefreshAssertionFieldNames();

            foreach (var assertion in value.MatchConfig.FieldAssertions)
            {
                EditingAssertions.Add(new FieldAssertion
                {
                    FieldName = assertion.FieldName,
                    Operator = assertion.Operator,
                    ExpectedValue = assertion.ExpectedValue
                });
            }

            if (value.MatchConfig.ResponseMode == ResponseBuildMode.ProtocolBuild)
            {
                RefreshMatchResponseFields();
            }
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
            TriggerProtocolName = MatchConfig.TriggerProtocolName,
            FieldAssertions = MatchConfig.FieldAssertions
                .Select(a => new FieldAssertion
                {
                    FieldName = a.FieldName,
                    Operator = a.Operator,
                    ExpectedValue = a.ExpectedValue
                }).ToList(),
            ResponseMode = MatchConfig.ResponseMode,
            ResponseContent = MatchConfig.ResponseContent,
            IsResponseHex = MatchConfig.IsResponseHex,
            ProtocolResponse = new ProtocolResponseConfig
            {
                ProtocolName = MatchConfig.ProtocolResponse.ProtocolName,
                FieldValues = new Dictionary<string, string>(MatchConfig.ProtocolResponse?.FieldValues ?? []),
                FieldHexModes = new Dictionary<string, bool>(MatchConfig.ProtocolResponse?.FieldHexModes ?? [])
            }
        } : null,
        SequentialConfig = SequentialConfig != null ? new SequentialRuleConfig
        {
            Frames = SequentialConfig.Frames.Select(f => new SequentialFrame
            {
                Id = f.Id, Name = f.Name, Content = f.Content,
                IsHexMode = f.IsHexMode, IsEnabled = f.IsEnabled,
                SortOrder = f.SortOrder, Description = f.Description,
                ResponseMode = f.ResponseMode,
                ProtocolResponse = new ProtocolResponseConfig
                {
                    ProtocolName = f.ProtocolResponse?.ProtocolName ?? string.Empty,
                    FieldValues = new Dictionary<string, string>(f.ProtocolResponse?.FieldValues ?? []),
                    FieldHexModes = new Dictionary<string, bool>(f.ProtocolResponse?.FieldHexModes ?? [])
                }
            }).ToList(),
            EnableLoop = SequentialConfig.EnableLoop,
            CurrentIndex = SequentialConfig.CurrentIndex
        } : null,
        ProtocolConfig = ProtocolConfig != null ? new ProtocolRuleConfig
        {
            ProtocolName = ProtocolConfig.ProtocolName,
            FieldValues = new Dictionary<string, string>(ProtocolConfig.FieldValues),
            FieldHexModes = new Dictionary<string, bool>(ProtocolConfig.FieldHexModes)
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
            TriggerProtocolName = model.MatchConfig.TriggerProtocolName,
            FieldAssertions = model.MatchConfig.FieldAssertions
                .Select(a => new FieldAssertion
                {
                    FieldName = a.FieldName,
                    Operator = a.Operator,
                    ExpectedValue = a.ExpectedValue
                }).ToList(),
            ResponseMode = model.MatchConfig.ResponseMode,
            ResponseContent = model.MatchConfig.ResponseContent,
            IsResponseHex = model.MatchConfig.IsResponseHex,
            ProtocolResponse = new ProtocolResponseConfig
            {
                ProtocolName = model.MatchConfig.ProtocolResponse?.ProtocolName ?? string.Empty,
                FieldValues = new Dictionary<string, string>(model.MatchConfig.ProtocolResponse?.FieldValues ?? []),
                FieldHexModes = new Dictionary<string, bool>(model.MatchConfig.ProtocolResponse?.FieldHexModes ?? [])
            }
        } : null,
        SequentialConfig = model.SequentialConfig != null ? new SequentialRuleConfig
        {
            Frames = model.SequentialConfig.Frames.Select(f => new SequentialFrame
            {
                Id = f.Id, Name = f.Name, Content = f.Content,
                IsHexMode = f.IsHexMode, IsEnabled = f.IsEnabled,
                SortOrder = f.SortOrder, Description = f.Description,
                ResponseMode = f.ResponseMode,
                ProtocolResponse = new ProtocolResponseConfig
                {
                    ProtocolName = f.ProtocolResponse?.ProtocolName ?? string.Empty,
                    FieldValues = new Dictionary<string, string>(f.ProtocolResponse?.FieldValues ?? []),
                    FieldHexModes = new Dictionary<string, bool>(f.ProtocolResponse?.FieldHexModes ?? [])
                }
            }).ToList(),
            EnableLoop = model.SequentialConfig.EnableLoop,
            CurrentIndex = model.SequentialConfig.CurrentIndex
        } : null,
        ProtocolConfig = model.ProtocolConfig != null ? new ProtocolRuleConfig
        {
            ProtocolName = model.ProtocolConfig.ProtocolName,
            FieldValues = new Dictionary<string, string>(model.ProtocolConfig.FieldValues),
            FieldHexModes = new Dictionary<string, bool>(model.ProtocolConfig.FieldHexModes)
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
    [ObservableProperty] private ResponseBuildMode _responseMode = ResponseBuildMode.PlainText;
    [ObservableProperty] private string _protocolName = string.Empty;

    /// <summary>
    /// 协议组帧时的字段值配置
    /// </summary>
    public Dictionary<string, string> ProtocolFieldValues { get; set; } = [];

    /// <summary>
    /// 协议组帧时的字段 Hex 模式标记
    /// </summary>
    public Dictionary<string, bool> ProtocolFieldHexModes { get; set; } = [];

    public SequentialFrame ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Content = Content,
        IsHexMode = IsHexMode,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        Description = Description,
        ResponseMode = ResponseMode,
        ProtocolResponse = new ProtocolResponseConfig
        {
            ProtocolName = ProtocolName,
            FieldValues = new Dictionary<string, string>(ProtocolFieldValues),
            FieldHexModes = new Dictionary<string, bool>(ProtocolFieldHexModes)
        }
    };

    public static SequentialFrameViewModel FromModel(SequentialFrame model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Content = model.Content,
        IsHexMode = model.IsHexMode,
        IsEnabled = model.IsEnabled,
        SortOrder = model.SortOrder,
        Description = model.Description,
        ResponseMode = model.ResponseMode,
        ProtocolName = model.ProtocolResponse?.ProtocolName ?? string.Empty,
        ProtocolFieldValues = new Dictionary<string, string>(model.ProtocolResponse?.FieldValues ?? []),
        ProtocolFieldHexModes = new Dictionary<string, bool>(model.ProtocolResponse?.FieldHexModes ?? [])
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
