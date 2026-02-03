namespace FlexComDotnet.Core.Features.Network.Models;

/// <summary>
/// 连接类型枚举
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// 串口连接
    /// </summary>
    Serial,

    /// <summary>
    /// TCP 客户端连接
    /// </summary>
    TcpClient,

    /// <summary>
    /// TCP 服务器
    /// </summary>
    TcpServer,

    /// <summary>
    /// UDP 单播/广播
    /// </summary>
    Udp
}
