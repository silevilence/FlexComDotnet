namespace FlexComDotnet.Core.Features.Protocol.Models.Dlt645;

/// <summary>
/// DL/T 645-2007 解析后的帧结果
/// </summary>
public class Dlt645ParsedFrame : ParsedFrame
{
    /// <summary>
    /// 电表地址 (12位BCD码)
    /// </summary>
    public string MeterAddress { get; set; } = string.Empty;

    /// <summary>
    /// 控制码
    /// </summary>
    public Dlt645ControlCode? ControlCode { get; set; }

    /// <summary>
    /// 数据域长度
    /// </summary>
    public int DataLength { get; set; }

    /// <summary>
    /// 数据标识 (4字节)
    /// </summary>
    public uint? DataIdentifier { get; set; }

    /// <summary>
    /// 数据标识信息
    /// </summary>
    public Dlt645DataIdentifier? DataIdentifierInfo { get; set; }

    /// <summary>
    /// 解析后的数据值
    /// </summary>
    public object? DataValue { get; set; }

    /// <summary>
    /// 格式化的数据值显示
    /// </summary>
    public string FormattedValue { get; set; } = string.Empty;

    /// <summary>
    /// 错误码 (异常应答时)
    /// </summary>
    public byte? ErrorByte { get; set; }

    /// <summary>
    /// 错误描述列表
    /// </summary>
    public List<string> ErrorDescriptions { get; set; } = [];

    /// <summary>
    /// 原始数据域 (已减33H还原)
    /// </summary>
    public byte[] DecodedDataField { get; set; } = [];
}
