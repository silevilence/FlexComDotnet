namespace FlexComDotnet.Core.Features.Network.Models;

/// <summary>
/// 连接状态枚举
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// 断开连接
    /// </summary>
    Disconnected,

    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 监听中 (仅用于 TCP Server)
    /// </summary>
    Listening,

    /// <summary>
    /// 错误状态
    /// </summary>
    Error
}
