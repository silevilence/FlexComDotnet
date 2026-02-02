namespace FlexComDotnet.Core.Features.Serial.Models;

/// <summary>
/// 应用配置模型，用于持久化用户设置
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 串口配置
    /// </summary>
    public SerialPortConfig SerialConfig { get; set; } = new();

    /// <summary>
    /// 显示配置
    /// </summary>
    public DisplayConfig DisplayConfig { get; set; } = new();

    /// <summary>
    /// 配置版本号，用于未来兼容性升级
    /// </summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// 显示配置模型
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// 接收区是否使用 HEX 显示模式
    /// </summary>
    public bool IsHexDisplayMode { get; set; }

    /// <summary>
    /// 是否显示时间戳
    /// </summary>
    public bool ShowTimestamp { get; set; }

    /// <summary>
    /// 是否自动换行
    /// </summary>
    public bool AutoLineBreak { get; set; } = true;

    /// <summary>
    /// 发送区是否使用 HEX 模式
    /// </summary>
    public bool IsHexSendMode { get; set; }
}
