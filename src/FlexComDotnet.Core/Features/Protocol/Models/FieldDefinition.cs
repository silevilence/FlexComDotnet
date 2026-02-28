namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 字段定义模型，描述帧中的一个数据字段
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// 字段名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字段描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 起始字节索引 (0-based)
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// 字节长度 (对于固定长度类型可自动推断)
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public DataType DataType { get; set; } = DataType.Bytes;

    /// <summary>
    /// 字节序
    /// </summary>
    public Endianness Endianness { get; set; } = Endianness.BigEndian;

    /// <summary>
    /// 位域定义列表 (可选，用于在字节内提取位)
    /// </summary>
    public List<BitFieldDefinition> BitFields { get; set; } = [];

    /// <summary>
    /// 是否启用此字段
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 获取数据类型的默认字节长度
    /// </summary>
    public static int GetDefaultLength(DataType dataType) => dataType switch
    {
        DataType.UInt8 or DataType.Int8 or DataType.Bool => 1,
        DataType.UInt16 or DataType.Int16 => 2,
        DataType.UInt32 or DataType.Int32 or DataType.Float => 4,
        DataType.UInt64 or DataType.Int64 or DataType.Double => 8,
        _ => 0 // Bytes 和 AsciiString 需要手动指定
    };
}

/// <summary>
/// 位域定义模型，用于在单个字节或多字节内提取位
/// </summary>
public class BitFieldDefinition
{
    /// <summary>
    /// 位域名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 位域描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 起始位索引 (0-based, 从 LSB 开始)
    /// </summary>
    public int BitOffset { get; set; }

    /// <summary>
    /// 位数
    /// </summary>
    public int BitCount { get; set; } = 1;

    /// <summary>
    /// 位掩码 (可选，优先于 BitOffset/BitCount)
    /// </summary>
    public byte? Mask { get; set; }

    /// <summary>
    /// 是否启用此位域
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
