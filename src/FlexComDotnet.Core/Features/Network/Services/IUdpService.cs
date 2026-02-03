using System.Net;
using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// UDP 服务接口
/// </summary>
public interface IUdpService : IConnection
{
    /// <summary>
    /// 当前配置
    /// </summary>
    UdpConfig? CurrentConfig { get; }

    /// <summary>
    /// 本地绑定端口
    /// </summary>
    int LocalPort { get; }

    /// <summary>
    /// 配置并绑定端口
    /// </summary>
    /// <param name="config">UDP 配置</param>
    /// <returns>是否成功</returns>
    Task<bool> BindAsync(UdpConfig config);

    /// <summary>
    /// 向指定端点发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="remoteEndPoint">远程端点</param>
    /// <returns>是否成功</returns>
    Task<bool> SendToAsync(byte[] data, IPEndPoint remoteEndPoint);

    /// <summary>
    /// 向指定地址和端口发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="host">远程主机</param>
    /// <param name="port">远程端口</param>
    /// <returns>是否成功</returns>
    Task<bool> SendToAsync(byte[] data, string host, int port);

    /// <summary>
    /// 广播数据到指定端口
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="port">广播端口</param>
    /// <returns>是否成功</returns>
    Task<bool> BroadcastAsync(byte[] data, int port);
}
