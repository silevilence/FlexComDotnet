using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Protocol.ViewModels;

/// <summary>
/// 协议解析器 ViewModel
/// </summary>
public partial class ProtocolParserViewModel : ObservableObject
{
    private readonly IProtocolParserService _parserService;
    private readonly IConfigurationService _configService;

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

    public IReadOnlyList<DataType> DataTypes { get; } = Enum.GetValues<DataType>();
    public IReadOnlyList<Endianness> EndianOptions { get; } = Enum.GetValues<Endianness>();
    public IReadOnlyList<ChecksumAlgorithmType> ChecksumAlgorithms { get; } = Enum.GetValues<ChecksumAlgorithmType>();

    public ProtocolParserViewModel(IProtocolParserService parserService, IConfigurationService configService)
    {
        _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
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
        EditingDefinition = new FrameDefinition
        {
            Name = "新协议",
            Description = "协议描述",
            MinFrameLength = 1
        };
        IsEditing = true;
        StatusMessage = "正在创建新协议定义...";
    }

    [RelayCommand]
    private void EditDefinition()
    {
        if (SelectedDefinition == null)
            return;

        EditingDefinition = CloneDefinition(SelectedDefinition);
        IsEditing = true;
        StatusMessage = $"正在编辑: {EditingDefinition.Name}";
    }

    [RelayCommand]
    private void SaveDefinition()
    {
        if (string.IsNullOrWhiteSpace(EditingDefinition.Name))
        {
            StatusMessage = "协议名称不能为空";
            return;
        }

        EditingDefinition.ModifiedAt = DateTime.Now;
        _parserService.RegisterDefinition(EditingDefinition);
        LoadDefinitions();
        SaveToConfig();
        IsEditing = false;
        StatusMessage = $"已保存: {EditingDefinition.Name}";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        StatusMessage = "已取消编辑";
    }

    [RelayCommand]
    private void DeleteDefinition()
    {
        if (SelectedDefinition == null)
            return;

        var name = SelectedDefinition.Name;
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
            Name = $"Field{EditingDefinition.Fields.Count + 1}",
            DataType = DataType.UInt8,
            StartIndex = EditingDefinition.Fields.Count > 0
                ? EditingDefinition.Fields.Max(f => f.StartIndex + f.Length)
                : 0
        };
        EditingDefinition.Fields.Add(newField);
        SelectedField = newField;
        OnPropertyChanged(nameof(EditingDefinition));
    }

    [RelayCommand]
    private void RemoveField()
    {
        if (SelectedField == null)
            return;

        EditingDefinition.Fields.Remove(SelectedField);
        SelectedField = null;
        OnPropertyChanged(nameof(EditingDefinition));
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
    private void EnableChecksum()
    {
        EditingDefinition.ChecksumConfig ??= new ChecksumConfig();
        OnPropertyChanged(nameof(EditingDefinition));
    }

    [RelayCommand]
    private void DisableChecksum()
    {
        EditingDefinition.ChecksumConfig = null;
        OnPropertyChanged(nameof(EditingDefinition));
    }

    [RelayCommand]
    private void EnableLengthField()
    {
        EditingDefinition.LengthFieldConfig ??= new LengthFieldConfig();
        OnPropertyChanged(nameof(EditingDefinition));
    }

    [RelayCommand]
    private void DisableLengthField()
    {
        EditingDefinition.LengthFieldConfig = null;
        OnPropertyChanged(nameof(EditingDefinition));
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
                BitFields = f.BitFields.Select(bf => new BitFieldDefinition
                {
                    Name = bf.Name,
                    Description = bf.Description,
                    BitOffset = bf.BitOffset,
                    BitCount = bf.BitCount,
                    Mask = bf.Mask,
                    IsEnabled = bf.IsEnabled
                }).ToList()
            }).ToList()
        };
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
}
