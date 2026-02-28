namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 解析后的帧结果
/// </summary>
public class ParsedFrame
{
    /// <summary>
    /// 原始帧数据
    /// </summary>
    public byte[] RawData { get; set; } = [];

    /// <summary>
    /// 使用的协议定义名称
    /// </summary>
    public string ProtocolName { get; set; } = string.Empty;

    /// <summary>
    /// 解析是否成功
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 校验是否通过
    /// </summary>
    public bool ChecksumValid { get; set; } = true;

    /// <summary>
    /// 解析错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 解析出的字段值列表
    /// </summary>
    public List<ParsedField> Fields { get; set; } = [];

    /// <summary>
    /// 解析时间戳
    /// </summary>
    public DateTime ParsedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 根据字段名获取解析值
    /// </summary>
    public ParsedField? GetField(string name) =>
        Fields.Find(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 根据字段名获取解析值 (泛型)
    /// </summary>
    public T? GetValue<T>(string name)
    {
        var field = GetField(name);
        if (field?.Value is T value)
            return value;
        return default;
    }
}

/// <summary>
/// 解析后的字段值
/// </summary>
public class ParsedField
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
    /// 解析后的值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 原始字节数据
    /// </summary>
    public byte[] RawBytes { get; set; } = [];

    /// <summary>
    /// 数据类型
    /// </summary>
    public DataType DataType { get; set; }

    /// <summary>
    /// 在帧中的起始索引
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// 字节长度
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// 位域解析结果 (如果有)
    /// </summary>
    public List<ParsedBitField> BitFields { get; set; } = [];

    /// <summary>
    /// 格式化显示值
    /// </summary>
    public string DisplayValue => Value?.ToString() ?? string.Empty;

    /// <summary>
    /// 十六进制显示
    /// </summary>
    public string HexValue => string.Join(" ", RawBytes.Select(b => b.ToString("X2")));
}

/// <summary>
/// 解析后的位域值
/// </summary>
public class ParsedBitField
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
    /// 解析后的值
    /// </summary>
    public ulong Value { get; set; }

    /// <summary>
    /// 布尔值表示 (用于单位标志)
    /// </summary>
    public bool BoolValue => Value != 0;

    /// <summary>
    /// 起始位
    /// </summary>
    public int BitOffset { get; set; }

    /// <summary>
    /// 位数
    /// </summary>
    public int BitCount { get; set; }
}
