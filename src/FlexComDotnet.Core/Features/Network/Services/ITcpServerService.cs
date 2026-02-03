using System.Net;
using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// TCP 服务器服务接口
/// </summary>
public interface ITcpServerService : IConnection
{
    /// <summary>
    /// 当前配置
    /// </summary>
    TcpServerConfig? CurrentConfig { get; }

    /// <summary>
    /// 当前已连接的客户端列表
    /// </summary>
    IReadOnlyList<ClientInfo> ConnectedClients { get; }

    /// <summary>
    /// 客户端连接事件
    /// </summary>
    event EventHandler<ClientInfo>? ClientConnected;

    /// <summary>
    /// 客户端断开事件
    /// </summary>
    event EventHandler<ClientInfo>? ClientDisconnected;

    /// <summary>
    /// 配置并开始监听
    /// </summary>
    /// <param name="config">TCP 服务器配置</param>
    /// <returns>是否成功</returns>
    Task<bool> StartAsync(TcpServerConfig config);

    /// <summary>
    /// 停止监听并断开所有客户端
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 向指定客户端发送数据
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <param name="data">要发送的数据</param>
    /// <returns>是否成功</returns>
    Task<bool> SendToClientAsync(string clientId, byte[] data);

    /// <summary>
    /// 向所有客户端广播数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <returns>成功发送的客户端数量</returns>
    Task<int> BroadcastAsync(byte[] data);

    /// <summary>
    /// 断开指定客户端
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    Task DisconnectClientAsync(string clientId);
}
