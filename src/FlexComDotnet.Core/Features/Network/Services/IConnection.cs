using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// 统一连接接口，屏蔽串口与网络 Socket 的底层差异
/// </summary>
public interface IConnection : IDisposable
{
    /// <summary>
    /// 连接类型
    /// </summary>
    ConnectionType ConnectionType { get; }

    /// <summary>
    /// 当前连接状态
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// 是否已连接/监听中
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 数据接收事件
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event EventHandler<ConnectionState>? StateChanged;

    /// <summary>
    /// 错误事件
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// 打开连接
    /// </summary>
    /// <returns>是否成功</returns>
    Task<bool> OpenAsync();

    /// <summary>
    /// 关闭连接
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <returns>是否成功</returns>
    Task<bool> SendAsync(byte[] data);
}
