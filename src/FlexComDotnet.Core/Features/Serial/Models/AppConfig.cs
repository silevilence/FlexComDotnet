using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Network.Models;

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
    /// 布局状态配置
    /// </summary>
    public LayoutState LayoutState { get; set; } = new();

    /// <summary>
    /// 自动回复配置
    /// </summary>
    public AutoReplyConfig AutoReplyConfig { get; set; } = new();

    /// <summary>
    /// 连接配置 (包含所有连接类型的配置)
    /// </summary>
    public ConnectionConfig ConnectionConfig { get; set; } = new();

    /// <summary>
    /// 配置版本号，用于未来兼容性升级
    /// </summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// 连接配置模型，包含所有连接类型的配置
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    /// 选中的连接类型
    /// </summary>
    public ConnectionType SelectedConnectionType { get; set; } = ConnectionType.Serial;

    /// <summary>
    /// TCP 客户端配置
    /// </summary>
    public TcpClientConfig TcpClientConfig { get; set; } = new();

    /// <summary>
    /// TCP 服务器配置
    /// </summary>
    public TcpServerConfig TcpServerConfig { get; set; } = new();

    /// <summary>
    /// UDP 配置
    /// </summary>
    public UdpConfig UdpConfig { get; set; } = new();
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
    /// 时间戳是否显示日期
    /// </summary>
    public bool ShowDateInTimestamp { get; set; }

    /// <summary>
    /// 是否自动换行
    /// </summary>
    public bool AutoLineBreak { get; set; } = true;

    /// <summary>
    /// 发送区是否使用 HEX 模式
    /// </summary>
    public bool IsHexSendMode { get; set; }

    /// <summary>
    /// 切换发送模式时是否转换内容
    /// </summary>
    public bool ConvertContentOnModeSwitch { get; set; } = true;

    /// <summary>
    /// 主题模式: 0=Light, 1=Dark, 2=System
    /// </summary>
    public int ThemeMode { get; set; } = 1; // 默认深色主题
}
