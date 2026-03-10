using CommunityToolkit.Mvvm.ComponentModel;

namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 帧组合测试的字段输入项
/// </summary>
public partial class FieldInputItem : ObservableObject
{
    /// <summary>
    /// 字段名称（用作 BuildFrame 的 key）
    /// </summary>
    [ObservableProperty]
    private string _fieldName = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>
    /// 字段描述
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    [ObservableProperty]
    private DataType _dataType = DataType.Bytes;

    /// <summary>
    /// 用户输入的值（字符串形式）
    /// </summary>
    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>
    /// 默认值
    /// </summary>
    [ObservableProperty]
    private string _defaultValue = string.Empty;

    /// <summary>
    /// 是否为 Hex 输入模式
    /// </summary>
    [ObservableProperty]
    private bool _isHexMode;

    /// <summary>
    /// 数据类型显示名称
    /// </summary>
    public string DataTypeDisplay => DataType switch
    {
        DataType.UInt8 => "UInt8",
        DataType.UInt16 => "UInt16",
        DataType.UInt32 => "UInt32",
        DataType.Int8 => "Int8",
        DataType.Int16 => "Int16",
        DataType.Int32 => "Int32",
        DataType.Float => "Float",
        DataType.Double => "Double",
        DataType.AsciiString => "ASCII",
        DataType.Bytes => "Bytes",
        DataType.Bool => "Bool",
        _ => DataType.ToString()
    };
}
