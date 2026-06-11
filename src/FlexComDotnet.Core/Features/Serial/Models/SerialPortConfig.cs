namespace FlexComDotnet.Core.Features.Serial.Models;

/// <summary>
/// 串口配置数据模型
/// </summary>
public class SerialPortConfig
{
    /// <summary>
    /// 串口名称 (如 COM1, COM2)
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// 波特率
    /// </summary>
    public BaudRate BaudRate { get; set; } = BaudRate.Baud115200;

    /// <summary>
    /// 数据位
    /// </summary>
    public DataBitsOption DataBits { get; set; } = DataBitsOption.Eight;

    /// <summary>
    /// 停止位
    /// </summary>
    public StopBitsOption StopBits { get; set; } = StopBitsOption.One;

    /// <summary>
    /// 校验位
    /// </summary>
    public ParityOption Parity { get; set; } = ParityOption.None;

    /// <summary>
    /// 流控制
    /// </summary>
    public FlowControlOption FlowControl { get; set; } = FlowControlOption.None;

    /// <summary>
    /// 帧间隔超时阈值（毫秒），相邻字节间隔超过此值则截断为帧。≥1
    /// </summary>
    public int FrameIntervalMs { get; set; } = 10;

    /// <summary>
    /// 最大帧长度（字节），单帧累积达到此值则强制截断。≥1
    /// </summary>
    public int MaxFrameBytes { get; set; } = 4096;

    /// <summary>
    /// 创建配置的副本
    /// </summary>
    public SerialPortConfig Clone() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        StopBits = StopBits,
        Parity = Parity,
        FlowControl = FlowControl,
        FrameIntervalMs = FrameIntervalMs,
        MaxFrameBytes = MaxFrameBytes
    };
}
