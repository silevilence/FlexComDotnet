namespace FlexComDotnet.Core.Features.Protocol.Models.ModbusRtu;

/// <summary>
/// Modbus-RTU 协议配置
/// </summary>
public class ModbusRtuConfig
{
    /// <summary>
    /// 从站地址 (1-247)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// 功能码
    /// </summary>
    public ModbusFunctionCode FunctionCode { get; set; } = ModbusFunctionCode.ReadHoldingRegisters;

    /// <summary>
    /// 起始地址 (用于读/写操作)
    /// </summary>
    public ushort StartAddress { get; set; }

    /// <summary>
    /// 寄存器数量 (用于读/写多个寄存器)
    /// </summary>
    public ushort Quantity { get; set; } = 1;
}
