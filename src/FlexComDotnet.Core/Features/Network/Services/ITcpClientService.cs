using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// TCP 客户端服务接口
/// </summary>
public interface ITcpClientService : IConnection
{
    /// <summary>
    /// 当前配置
    /// </summary>
    TcpClientConfig? CurrentConfig { get; }

    /// <summary>
    /// 配置并打开连接
    /// </summary>
    /// <param name="config">TCP 客户端配置</param>
    /// <returns>是否成功</returns>
    Task<bool> ConnectAsync(TcpClientConfig config);
}
