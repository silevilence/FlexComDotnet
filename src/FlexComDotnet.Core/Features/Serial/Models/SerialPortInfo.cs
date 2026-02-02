namespace FlexComDotnet.Core.Features.Serial.Models;

/// <summary>
/// 串口信息
/// </summary>
public class SerialPortInfo
{
    /// <summary>
    /// 串口名称 (如 COM1)
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// 串口描述 (如 USB-SERIAL CH340)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Description) 
        ? PortName 
        : $"{PortName} - {Description}";

    public override string ToString() => DisplayName;
}
