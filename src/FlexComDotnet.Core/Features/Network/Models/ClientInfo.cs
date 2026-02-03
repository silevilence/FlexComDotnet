using System.Net;

namespace FlexComDotnet.Core.Features.Network.Models;

/// <summary>
/// TCP 服务器客户端信息
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// 客户端唯一标识
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 远程端点
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectedTime { get; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivityTime { get; set; }

    /// <summary>
    /// 已接收字节数
    /// </summary>
    public long ReceivedBytes { get; set; }

    /// <summary>
    /// 已发送字节数
    /// </summary>
    public long SentBytes { get; set; }

    public ClientInfo(string id, IPEndPoint remoteEndPoint)
    {
        Id = id;
        RemoteEndPoint = remoteEndPoint;
        ConnectedTime = DateTime.Now;
        LastActivityTime = DateTime.Now;
    }

    public override string ToString() => $"{Id}: {RemoteEndPoint}";
}
