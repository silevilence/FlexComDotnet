namespace FlexComDotnet.Core.Features.Protocol.Models.ModbusRtu;

/// <summary>
/// Modbus-RTU 协议解析结果
/// </summary>
public class ModbusRtuParsedFrame : ParsedFrame
{
    /// <summary>
    /// 从站地址
    /// </summary>
    public byte SlaveId { get; set; }

    /// <summary>
    /// 功能码原始值
    /// </summary>
    public byte FunctionCodeRaw { get; set; }

    /// <summary>
    /// 功能码
    /// </summary>
    public ModbusFunctionCode FunctionCode { get; set; }

    /// <summary>
    /// 是否为异常响应
    /// </summary>
    public bool IsExceptionResponse { get; set; }

    /// <summary>
    /// 是否为响应帧（false 表示请求帧）
    /// </summary>
    public bool IsResponseFrame { get; set; }

    /// <summary>
    /// 异常码 (仅异常响应时有效)
    /// </summary>
    public byte? ExceptionCode { get; set; }

    /// <summary>
    /// 异常描述
    /// </summary>
    public string? ExceptionDescription { get; set; }

    /// <summary>
    /// 起始地址 (请求帧中)
    /// </summary>
    public ushort? StartAddress { get; set; }

    /// <summary>
    /// 寄存器数量 (请求帧中)
    /// </summary>
    public ushort? Quantity { get; set; }

    /// <summary>
    /// 数据字节数 (响应帧中)
    /// </summary>
    public byte? ByteCount { get; set; }

    /// <summary>
    /// 寄存器数据区 (已提取，不含帧头和CRC)
    /// </summary>
    public byte[] RegisterData { get; set; } = [];
}
