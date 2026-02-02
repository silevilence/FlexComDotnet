namespace FlexComDotnet.Core.Features.Serial.Models;

/// <summary>
/// 常用波特率
/// </summary>
public enum BaudRate
{
    Baud1200 = 1200,
    Baud2400 = 2400,
    Baud4800 = 4800,
    Baud9600 = 9600,
    Baud19200 = 19200,
    Baud38400 = 38400,
    Baud57600 = 57600,
    Baud115200 = 115200,
    Baud230400 = 230400,
    Baud460800 = 460800,
    Baud921600 = 921600
}

/// <summary>
/// 数据位选项
/// </summary>
public enum DataBitsOption
{
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8
}

/// <summary>
/// 停止位选项
/// </summary>
public enum StopBitsOption
{
    /// <summary>
    /// 1 位停止位
    /// </summary>
    One = 1,
    
    /// <summary>
    /// 1.5 位停止位
    /// </summary>
    OnePointFive = 3,
    
    /// <summary>
    /// 2 位停止位
    /// </summary>
    Two = 2
}

/// <summary>
/// 校验位选项
/// </summary>
public enum ParityOption
{
    /// <summary>
    /// 无校验
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 奇校验
    /// </summary>
    Odd = 1,
    
    /// <summary>
    /// 偶校验
    /// </summary>
    Even = 2,
    
    /// <summary>
    /// 标记校验
    /// </summary>
    Mark = 3,
    
    /// <summary>
    /// 空格校验
    /// </summary>
    Space = 4
}

/// <summary>
/// 流控制选项
/// </summary>
public enum FlowControlOption
{
    /// <summary>
    /// 无流控
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 软件流控 (XON/XOFF)
    /// </summary>
    XonXoff = 1,
    
    /// <summary>
    /// 硬件流控 (RTS/CTS)
    /// </summary>
    RtsCts = 2,
    
    /// <summary>
    /// 硬件流控 (DTR/DSR)
    /// </summary>
    DtrDsr = 3
}

/// <summary>
/// 校验和类型
/// </summary>
public enum ChecksumType
{
    /// <summary>
    /// 不追加校验和
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Sum8 累加和校验
    /// </summary>
    Sum8 = 1,
    
    /// <summary>
    /// CRC16 MODBUS 校验
    /// </summary>
    Crc16Modbus = 2
}
