using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Protocol.ViewModels;

/// <summary>
/// 协议解析器 ViewModel
/// </summary>
public partial class ProtocolParserViewModel : ObservableObject
{
    private readonly IProtocolParserService _parserService;
    private readonly IConfigurationService _configService;
    private readonly IScriptManager? _scriptManager;

    [ObservableProperty]
    private ObservableCollection<FrameDefinition> _definitions = [];

    [ObservableProperty]
    private FrameDefinition? _selectedDefinition;

    [ObservableProperty]
    private FrameDefinition _editingDefinition = new();

    [ObservableProperty]
    private FieldDefinition? _selectedField;

    [ObservableProperty]
    private string _testFrameHex = string.Empty;

    [ObservableProperty]
    private ParsedFrame? _parseResult;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FieldDefinition> _editingFields = [];

    public bool HasLengthFieldConfig => EditingDefinition.LengthFieldConfig != null;
    public bool HasChecksumConfig => EditingDefinition.ChecksumConfig != null;
    public bool IsGenericProtocol => EditingDefinition.ProtocolType == ProtocolType.Generic;
    public bool IsDlt645Protocol => EditingDefinition.ProtocolType == ProtocolType.Dlt645;

    public IReadOnlyList<DataType> DataTypes { get; } = Enum.GetValues<DataType>();
    public IReadOnlyList<Endianness> EndianOptions { get; } = Enum.GetValues<Endianness>();
    public IReadOnlyList<ChecksumAlgorithmType> ChecksumAlgorithms { get; } = Enum.GetValues<ChecksumAlgorithmType>();
    public IReadOnlyList<ProtocolType> ProtocolTypes { get; } = Enum.GetValues<ProtocolType>();

    /// <summary>
    /// 帧组合测试 - 字段输入项
    /// </summary>
    public ObservableCollection<FieldInputItem> BuildFieldInputs { get; } = [];

    /// <summary>
    /// 帧组合测试 - 构建结果（Hex字符串）
    /// </summary>
    [ObservableProperty]
    private string _buildResultHex = string.Empty;

    /// <summary>
    /// 帧组合测试 - 构建状态消息
    /// </summary>
    [ObservableProperty]
    private string _buildStatusMessage = string.Empty;

    /// <summary>
    /// 帧组合测试 - 使用的协议名称
    /// </summary>
    [ObservableProperty]
    private FrameDefinition? _buildSelectedDefinition;

    /// <summary>
    /// 编辑前的协议定义快照（用于脏状态检测）
    /// </summary>
    private string? _editingSnapshot;

    /// <summary>
    /// 当前编辑是否已被修改（脏状态）
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    /// 协议保存拦截事件 - 当协议被脚本引用时触发
    /// UI层应弹出确认对话框，返回用户选择的操作
    /// </summary>
    public event Func<string, List<string>, Task<ProtocolSaveAction>>? SaveInterceptRequested;

    /// <summary>
    /// 协议删除拦截事件 - 当协议被脚本引用时触发
    /// UI层应弹出确认对话框，返回用户是否确认删除
    /// </summary>
    public event Func<string, List<string>, Task<bool>>? DeleteInterceptRequested;

    public ProtocolParserViewModel(IProtocolParserService parserService, IConfigurationService configService, IScriptManager? scriptManager = null)
    {
        _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _scriptManager = scriptManager;
        LoadDefinitions();
    }

    private void LoadDefinitions()
    {
        Definitions.Clear();
        foreach (var def in _parserService.GetAllDefinitions())
        {
            Definitions.Add(def);
        }
    }

    private void SaveToConfig()
    {
        var config = _configService.Load();
        config.ProtocolDefinitions = _parserService.GetAllDefinitions().ToList();
        _configService.Save(config);
    }

    [RelayCommand]
    private void NewDefinition()
    {
        if (IsEditing && IsDirty)
        {
            StatusMessage = "请先保存或取消当前编辑";
            return;
        }

        EditingDefinition = new FrameDefinition
        {
            Name = "新协议",
            Description = "协议描述",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };
        _editingSnapshot = SerializeDefinitionSnapshot(EditingDefinition);
        SyncEditingFields();
        IsEditing = true;
        IsDirty = false;
        OnPropertyChanged(nameof(IsGenericProtocol));
        OnPropertyChanged(nameof(IsDlt645Protocol));
        StatusMessage = "正在创建新协议定义...";
    }

    [RelayCommand]
    private void NewDlt645Definition()
    {
        if (IsEditing && IsDirty)
        {
            StatusMessage = "请先保存或取消当前编辑";
            return;
        }

        EditingDefinition = new FrameDefinition
        {
            Name = "DL/T 645-2007",
            Description = "电表通信协议",
            ProtocolType = ProtocolType.Dlt645,
            Header = "68",
            Trailer = "16",
            MinFrameLength = 12,
            MaxFrameLength = 256
        };
        _editingSnapshot = SerializeDefinitionSnapshot(EditingDefinition);
        SyncEditingFields();
        IsEditing = true;
        IsDirty = false;
        OnPropertyChanged(nameof(IsGenericProtocol));
        OnPropertyChanged(nameof(IsDlt645Protocol));
        StatusMessage = "正在创建 DL/T 645-2007 协议定义...";
    }

    [RelayCommand]
    private void EditDefinition()
    {
        if (SelectedDefinition == null)
            return;

        if (IsEditing && IsDirty)
        {
            // 如果当前正在编辑且有未保存的修改，不直接切换
            StatusMessage = "请先保存或取消当前编辑";
            return;
        }

        EditingDefinition = CloneDefinition(SelectedDefinition);
        _editingSnapshot = SerializeDefinitionSnapshot(EditingDefinition);
        SyncEditingFields();
        IsEditing = true;
        IsDirty = false;
        OnPropertyChanged(nameof(IsGenericProtocol));
        OnPropertyChanged(nameof(IsDlt645Protocol));
        StatusMessage = $"正在编辑: {EditingDefinition.Name}";
    }

    /// <summary>
    /// 双击协议列表项进入编辑状态
    /// </summary>
    [RelayCommand]
    private void DoubleClickDefinition()
    {
        if (SelectedDefinition == null)
            return;

        if (IsEditing && IsDirty)
        {
            // 脏状态时不直接切换，需要UI层弹出确认对话框
            StatusMessage = "当前编辑已修改，请先保存或取消";
            return;
        }

        EditingDefinition = CloneDefinition(SelectedDefinition);
        _editingSnapshot = SerializeDefinitionSnapshot(EditingDefinition);
        SyncEditingFields();
        IsEditing = true;
        IsDirty = false;
        OnPropertyChanged(nameof(IsGenericProtocol));
        OnPropertyChanged(nameof(IsDlt645Protocol));
        StatusMessage = $"正在编辑: {EditingDefinition.Name}";
    }

    /// <summary>
    /// 强制切换编辑（放弃当前修改）
    /// </summary>
    [RelayCommand]
    private void ForceEditDefinition(FrameDefinition? definition)
    {
        if (definition == null)
            return;

        EditingDefinition = CloneDefinition(definition);
        _editingSnapshot = SerializeDefinitionSnapshot(EditingDefinition);
        SyncEditingFields();
        IsEditing = true;
        IsDirty = false;
        OnPropertyChanged(nameof(IsGenericProtocol));
        OnPropertyChanged(nameof(IsDlt645Protocol));
        StatusMessage = $"正在编辑: {EditingDefinition.Name}";
    }

    private void SyncEditingFields()
    {
        EditingFields.Clear();
        foreach (var field in EditingDefinition.Fields)
        {
            EditingFields.Add(field);
        }
        OnPropertyChanged(nameof(HasLengthFieldConfig));
        OnPropertyChanged(nameof(HasChecksumConfig));
    }

    [RelayCommand]
    private async Task SaveDefinitionAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingDefinition.Name))
        {
            StatusMessage = "协议名称不能为空";
            return;
        }

        // 依赖检查：检查是否有脚本引用了此协议
        var referencingScripts = FindScriptsReferencingProtocol(EditingDefinition.Name);
        if (referencingScripts.Count > 0 && SaveInterceptRequested != null)
        {
            var action = await SaveInterceptRequested.Invoke(EditingDefinition.Name, referencingScripts);
            if (action == ProtocolSaveAction.Cancel)
            {
                StatusMessage = "已取消保存";
                return;
            }
            if (action == ProtocolSaveAction.CloneAsNew)
            {
                // 克隆模式：另存为新协议，原协议不变
                EditingDefinition.Name = EditingDefinition.Name + " (副本)";
                EditingDefinition.CreatedAt = DateTime.Now;
            }
        }

        EditingDefinition.Fields = EditingFields.ToList();
        EditingDefinition.ModifiedAt = DateTime.Now;
        _parserService.RegisterDefinition(EditingDefinition);
        LoadDefinitions();
        SaveToConfig();
        IsEditing = false;
        IsDirty = false;
        _editingSnapshot = null;
        StatusMessage = $"已保存: {EditingDefinition.Name}";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsDirty = false;
        _editingSnapshot = null;
        StatusMessage = "已取消编辑";
    }

    [RelayCommand]
    private async Task DeleteDefinitionAsync()
    {
        if (SelectedDefinition == null)
            return;

        var name = SelectedDefinition.Name;

        // 依赖检查：检查是否有脚本引用了此协议
        var referencingScripts = FindScriptsReferencingProtocol(name);
        if (referencingScripts.Count > 0 && DeleteInterceptRequested != null)
        {
            var confirmed = await DeleteInterceptRequested.Invoke(name, referencingScripts);
            if (!confirmed)
            {
                StatusMessage = "已取消删除";
                return;
            }
        }

        if (_parserService.RemoveParser(name))
        {
            LoadDefinitions();
            SaveToConfig();
            StatusMessage = $"已删除: {name}";
        }
    }

    [RelayCommand]
    private void AddField()
    {
        var newField = new FieldDefinition
        {
            Name = $"Field{EditingFields.Count + 1}",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = EditingFields.Count > 0
                ? EditingFields.Max(f => f.StartIndex + Math.Max(f.Length, 1))
                : 0
        };
        EditingFields.Add(newField);
        SelectedField = newField;
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveField()
    {
        if (SelectedField == null)
            return;

        EditingFields.Remove(SelectedField);
        SelectedField = null;
        MarkDirty();
    }

    [RelayCommand]
    private void AddBitField()
    {
        if (SelectedField == null)
            return;

        var newBitField = new BitFieldDefinition
        {
            Name = $"Bit{SelectedField.BitFields.Count}",
            BitOffset = 0,
            BitCount = 1
        };
        SelectedField.BitFields.Add(newBitField);
        OnPropertyChanged(nameof(SelectedField));
    }

    [RelayCommand]
    private void RemoveBitField(BitFieldDefinition? bitField)
    {
        if (SelectedField == null || bitField == null)
            return;

        SelectedField.BitFields.Remove(bitField);
        OnPropertyChanged(nameof(SelectedField));
    }

    [RelayCommand]
    private void TestParse()
    {
        if (string.IsNullOrWhiteSpace(TestFrameHex))
        {
            StatusMessage = "请输入测试帧数据";
            return;
        }

        try
        {
            var bytes = HexStringToBytes(TestFrameHex);
            if (bytes.Length == 0)
            {
                StatusMessage = "无效的十六进制数据";
                return;
            }

            if (IsEditing)
            {
                var tempParser = _parserService.RegisterDefinition(EditingDefinition);
                ParseResult = tempParser.Parse(bytes);
            }
            else if (SelectedDefinition != null)
            {
                ParseResult = _parserService.Parse(SelectedDefinition.Name, bytes);
            }
            else
            {
                ParseResult = _parserService.AutoParse(bytes);
            }

            StatusMessage = ParseResult?.IsValid == true
                ? $"解析成功: {ParseResult.Fields.Count} 个字段"
                : $"解析失败: {ParseResult?.ErrorMessage ?? "未知错误"}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"解析错误: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleLengthField()
    {
        if (EditingDefinition.LengthFieldConfig == null)
        {
            EditingDefinition.LengthFieldConfig = new LengthFieldConfig();
        }
        else
        {
            EditingDefinition.LengthFieldConfig = null;
        }
        OnPropertyChanged(nameof(HasLengthFieldConfig));
        OnPropertyChanged(nameof(EditingDefinition));
        MarkDirty();
    }

    [RelayCommand]
    private void ToggleChecksum()
    {
        if (EditingDefinition.ChecksumConfig == null)
        {
            EditingDefinition.ChecksumConfig = new ChecksumConfig();
        }
        else
        {
            EditingDefinition.ChecksumConfig = null;
        }
        OnPropertyChanged(nameof(HasChecksumConfig));
        OnPropertyChanged(nameof(EditingDefinition));
        MarkDirty();
    }

    [RelayCommand]
    private async Task ExportDefinitionAsync()
    {
        if (SelectedDefinition == null)
            return;

        var fileName = $"{SelectedDefinition.Name}.json";
        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FlexComDotnet",
            "Protocols",
            fileName);

        await _parserService.SaveDefinitionAsync(SelectedDefinition, filePath);
        StatusMessage = $"已导出到: {filePath}";
    }

    [RelayCommand]
    private async Task ImportDefinitionAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var definition = await _parserService.LoadDefinitionAsync(filePath);
        if (definition != null)
        {
            _parserService.RegisterDefinition(definition);
            LoadDefinitions();
            StatusMessage = $"已导入: {definition.Name}";
        }
        else
        {
            StatusMessage = "导入失败: 无法读取文件";
        }
    }

    private static FrameDefinition CloneDefinition(FrameDefinition source)
    {
        return new FrameDefinition
        {
            ProtocolType = source.ProtocolType,
            Name = source.Name,
            Description = source.Description,
            Header = source.Header,
            Trailer = source.Trailer,
            MinFrameLength = source.MinFrameLength,
            MaxFrameLength = source.MaxFrameLength,
            IsEnabled = source.IsEnabled,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            ChecksumConfig = source.ChecksumConfig != null ? new ChecksumConfig
            {
                Algorithm = source.ChecksumConfig.Algorithm,
                StartIndex = source.ChecksumConfig.StartIndex,
                Length = source.ChecksumConfig.Length,
                CalculateStartIndex = source.ChecksumConfig.CalculateStartIndex,
                CalculateEndIndex = source.ChecksumConfig.CalculateEndIndex,
                Endianness = source.ChecksumConfig.Endianness
            } : null,
            LengthFieldConfig = source.LengthFieldConfig != null ? new LengthFieldConfig
            {
                StartIndex = source.LengthFieldConfig.StartIndex,
                Length = source.LengthFieldConfig.Length,
                Endianness = source.LengthFieldConfig.Endianness,
                IncludesLengthField = source.LengthFieldConfig.IncludesLengthField,
                IncludesHeader = source.LengthFieldConfig.IncludesHeader,
                IncludesChecksum = source.LengthFieldConfig.IncludesChecksum,
                Offset = source.LengthFieldConfig.Offset
            } : null,
            Fields = source.Fields.Select(f => new FieldDefinition
            {
                Name = f.Name,
                Description = f.Description,
                StartIndex = f.StartIndex,
                Length = f.Length,
                DataType = f.DataType,
                Endianness = f.Endianness,
                IsEnabled = f.IsEnabled,
                BitFields = new ObservableCollection<BitFieldDefinition>(f.BitFields.Select(bf => new BitFieldDefinition
                {
                    Name = bf.Name,
                    Description = bf.Description,
                    BitOffset = bf.BitOffset,
                    BitCount = bf.BitCount,
                    Mask = bf.Mask,
                    IsEnabled = bf.IsEnabled
                }))
            }).ToList()
        };
    }

    /// <summary>
    /// 构建测试帧
    /// </summary>
    [RelayCommand]
    private void BuildTestFrame()
    {
        if (BuildSelectedDefinition == null)
        {
            BuildStatusMessage = "请先选择协议";
            return;
        }

        try
        {
            var parser = _parserService.GetParser(BuildSelectedDefinition.Name);
            if (parser == null)
            {
                BuildStatusMessage = "未找到对应的解析器";
                return;
            }

            var fieldValues = new Dictionary<string, object>();
            foreach (var input in BuildFieldInputs)
            {
                if (!string.IsNullOrEmpty(input.Value))
                {
                    fieldValues[input.FieldName] = input.IsHexMode
                        ? (object)HexHelper.HexStringToBytes(input.Value.Replace(" ", ""))
                        : input.Value;
                }
            }

            var frame = parser.BuildFrame(fieldValues);
            BuildResultHex = string.Join(" ", frame.Select(b => b.ToString("X2")));
            BuildStatusMessage = $"帧构建成功: {frame.Length} 字节";
        }
        catch (Exception ex)
        {
            BuildStatusMessage = $"帧构建失败: {ex.Message}";
            BuildResultHex = string.Empty;
        }
    }

    /// <summary>
    /// 刷新帧组合输入字段（当选择协议变化时）
    /// </summary>
    [RelayCommand]
    private void RefreshBuildInputs()
    {
        BuildFieldInputs.Clear();
        BuildResultHex = string.Empty;
        BuildStatusMessage = string.Empty;

        if (BuildSelectedDefinition == null)
            return;

        // Add protocol-specific fixed fields for DL/T 645
        if (BuildSelectedDefinition.ProtocolType == ProtocolType.Dlt645)
        {
            BuildFieldInputs.Add(new FieldInputItem
            {
                FieldName = "电表地址",
                DisplayName = "电表地址",
                Description = "12位BCD码地址",
                DataType = DataType.AsciiString,
                DefaultValue = "000000000000"
            });
            BuildFieldInputs.Add(new FieldInputItem
            {
                FieldName = "控制码",
                DisplayName = "控制码",
                Description = "功能控制字节 (Hex)",
                DataType = DataType.UInt8,
                DefaultValue = "11"
            });
            BuildFieldInputs.Add(new FieldInputItem
            {
                FieldName = "数据标识",
                DisplayName = "数据标识",
                Description = "4字节数据标识 (十进制)",
                DataType = DataType.UInt32,
                DefaultValue = "65536"
            });
        }

        // Add user-defined fields
        foreach (var field in BuildSelectedDefinition.Fields.Where(f => f.IsEnabled))
        {
            BuildFieldInputs.Add(new FieldInputItem
            {
                FieldName = field.Name,
                DisplayName = field.Name,
                Description = field.Description,
                DataType = field.DataType,
                DefaultValue = string.Empty,
                IsHexMode = field.DataType is DataType.Bytes or DataType.UInt8
            });
        }
    }

    partial void OnBuildSelectedDefinitionChanged(FrameDefinition? value)
    {
        RefreshBuildInputs();
    }

    /// <summary>
    /// 标记编辑为脏状态
    /// </summary>
    public void MarkDirty()
    {
        if (IsEditing && _editingSnapshot != null)
        {
            IsDirty = true;
        }
    }

    /// <summary>
    /// 检测当前编辑是否有修改
    /// </summary>
    public bool CheckDirty()
    {
        if (!IsEditing || _editingSnapshot == null)
            return false;

        var current = SerializeDefinitionSnapshot(EditingDefinition);
        IsDirty = current != _editingSnapshot;
        return IsDirty;
    }

    private static string SerializeDefinitionSnapshot(FrameDefinition def)
    {
        // Simple serialization for comparison
        var parts = new List<string>
        {
            def.Name,
            def.Description,
            def.Header,
            def.Trailer,
            def.MinFrameLength.ToString(),
            def.MaxFrameLength.ToString(),
            def.ProtocolType.ToString(),
            def.ChecksumConfig?.Algorithm.ToString() ?? "",
            def.LengthFieldConfig?.StartIndex.ToString() ?? ""
        };

        foreach (var field in def.Fields)
        {
            parts.Add($"{field.Name}|{field.StartIndex}|{field.Length}|{field.DataType}|{field.Endianness}|{field.IsEnabled}");
        }

        return string.Join(";;", parts);
    }

    private static byte[] HexStringToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "");
        if (hex.Length % 2 != 0)
            return [];

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                return [];
        }
        return bytes;
    }

    /// <summary>
    /// 查找引用了指定协议的脚本列表
    /// </summary>
    private List<string> FindScriptsReferencingProtocol(string protocolName)
    {
        if (_scriptManager == null)
            return [];

        var referencingScripts = new List<string>();
        foreach (var script in _scriptManager.GetAllScripts())
        {
            var content = _scriptManager.ReadScriptContent(script.Id);
            if (content != null && content.Contains(protocolName, StringComparison.OrdinalIgnoreCase))
            {
                referencingScripts.Add(script.Name);
            }
        }

        return referencingScripts;
    }
}
