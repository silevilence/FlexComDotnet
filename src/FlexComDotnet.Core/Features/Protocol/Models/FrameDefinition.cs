using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 帧结构定义模型
/// </summary>
public class FrameDefinition
{
    /// <summary>
    /// 协议名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 协议描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 帧头 (十六进制字符串，如 "AA BB")
    /// </summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// 帧尾 (十六进制字符串，可选)
    /// </summary>
    public string Trailer { get; set; } = string.Empty;

    /// <summary>
    /// 校验配置
    /// </summary>
    public ChecksumConfig? ChecksumConfig { get; set; }

    /// <summary>
    /// 长度字段配置 (可选，用于变长帧)
    /// </summary>
    public LengthFieldConfig? LengthFieldConfig { get; set; }

    /// <summary>
    /// 数据字段定义列表
    /// </summary>
    public List<FieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// 最小帧长度 (字节)
    /// </summary>
    public int MinFrameLength { get; set; }

    /// <summary>
    /// 最大帧长度 (字节，0 表示不限制)
    /// </summary>
    public int MaxFrameLength { get; set; }

    /// <summary>
    /// 是否启用此协议定义
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 校验配置
/// </summary>
public class ChecksumConfig
{
    /// <summary>
    /// 校验算法类型
    /// </summary>
    public ChecksumAlgorithmType Algorithm { get; set; } = ChecksumAlgorithmType.Crc16Modbus;

    /// <summary>
    /// 校验值起始索引 (负数表示从末尾倒数)
    /// </summary>
    public int StartIndex { get; set; } = -2;

    /// <summary>
    /// 校验值长度
    /// </summary>
    public int Length { get; set; } = 2;

    /// <summary>
    /// 校验计算范围起始索引
    /// </summary>
    public int CalculateStartIndex { get; set; }

    /// <summary>
    /// 校验计算范围结束索引 (负数表示从末尾倒数，不包含校验位)
    /// </summary>
    public int CalculateEndIndex { get; set; } = -2;

    /// <summary>
    /// 校验值字节序
    /// </summary>
    public Endianness Endianness { get; set; } = Endianness.LittleEndian;
}

/// <summary>
/// 长度字段配置
/// </summary>
public class LengthFieldConfig
{
    /// <summary>
    /// 长度字段起始索引
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// 长度字段字节数
    /// </summary>
    public int Length { get; set; } = 1;

    /// <summary>
    /// 长度字段字节序
    /// </summary>
    public Endianness Endianness { get; set; } = Endianness.BigEndian;

    /// <summary>
    /// 长度值是否包含长度字段本身
    /// </summary>
    public bool IncludesLengthField { get; set; }

    /// <summary>
    /// 长度值是否包含帧头
    /// </summary>
    public bool IncludesHeader { get; set; }

    /// <summary>
    /// 长度值是否包含校验位
    /// </summary>
    public bool IncludesChecksum { get; set; }

    /// <summary>
    /// 长度偏移量 (实际帧长度 = 长度字段值 + Offset)
    /// </summary>
    public int Offset { get; set; }
}
