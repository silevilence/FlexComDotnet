namespace FlexComDotnet.Core.Features.Protocol.Models.ModbusRtu;

/// <summary>
/// Modbus-RTU 功能码枚举
/// </summary>
public enum ModbusFunctionCode : byte
{
    /// <summary>
    /// 读保持寄存器 (0x03)
    /// </summary>
    ReadHoldingRegisters = 0x03,

    /// <summary>
    /// 读输入寄存器 (0x04)
    /// </summary>
    ReadInputRegisters = 0x04,

    /// <summary>
    /// 写单个寄存器 (0x06)
    /// </summary>
    WriteSingleRegister = 0x06,

    /// <summary>
    /// 写多个寄存器 (0x10)
    /// </summary>
    WriteMultipleRegisters = 0x10
}

/// <summary>
/// Modbus 功能码扩展方法
/// </summary>
public static class ModbusFunctionCodeExtensions
{
    /// <summary>
    /// 获取功能码的中文描述
    /// </summary>
    public static string GetDescription(this ModbusFunctionCode code) => code switch
    {
        ModbusFunctionCode.ReadHoldingRegisters => "读保持寄存器",
        ModbusFunctionCode.ReadInputRegisters => "读输入寄存器",
        ModbusFunctionCode.WriteSingleRegister => "写单个寄存器",
        ModbusFunctionCode.WriteMultipleRegisters => "写多个寄存器",
        _ => $"未知功能码 (0x{(byte)code:X2})"
    };

    /// <summary>
    /// 判断是否为读操作
    /// </summary>
    public static bool IsReadOperation(this ModbusFunctionCode code) =>
        code is ModbusFunctionCode.ReadHoldingRegisters or ModbusFunctionCode.ReadInputRegisters;

    /// <summary>
    /// 判断是否为写操作
    /// </summary>
    public static bool IsWriteOperation(this ModbusFunctionCode code) =>
        code is ModbusFunctionCode.WriteSingleRegister or ModbusFunctionCode.WriteMultipleRegisters;

    /// <summary>
    /// 判断功能码是否有效
    /// </summary>
    public static bool IsValid(byte code) =>
        code is 0x03 or 0x04 or 0x06 or 0x10;

    /// <summary>
    /// 判断是否为异常响应功能码 (最高位为1)
    /// </summary>
    public static bool IsExceptionResponse(byte code) => (code & 0x80) != 0;

    /// <summary>
    /// 获取异常码描述
    /// </summary>
    public static string GetExceptionDescription(byte exceptionCode) => exceptionCode switch
    {
        0x01 => "非法功能码",
        0x02 => "非法数据地址",
        0x03 => "非法数据值",
        0x04 => "从站设备故障",
        0x05 => "确认 (长时间处理)",
        0x06 => "从站设备忙",
        0x08 => "存储奇偶校验错",
        0x0A => "网关路径不可用",
        0x0B => "网关目标设备无响应",
        _ => $"未知异常 (0x{exceptionCode:X2})"
    };
}
