using System.Net;

namespace FlexComDotnet.Core.Features.Network.Models;

/// <summary>
/// 网络连接配置基类
/// </summary>
public abstract class NetworkConfig
{
    /// <summary>
    /// 连接类型
    /// </summary>
    public abstract ConnectionType ConnectionType { get; }
}

/// <summary>
/// TCP 客户端配置
/// </summary>
public class TcpClientConfig : NetworkConfig
{
    /// <inheritdoc/>
    public override ConnectionType ConnectionType => ConnectionType.TcpClient;

    /// <summary>
    /// 远程主机地址
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// 远程主机端口
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 连接超时时间 (毫秒)
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 接收超时时间 (毫秒), 0 表示无限等待
    /// </summary>
    public int ReceiveTimeout { get; set; } = 0;

    /// <summary>
    /// 发送超时时间 (毫秒), 0 表示无限等待
    /// </summary>
    public int SendTimeout { get; set; } = 0;

    /// <summary>
    /// 是否启用 KeepAlive
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// 是否禁用 Nagle 算法 (启用后减少小包延迟)
    /// </summary>
    public bool NoDelay { get; set; } = false;

    /// <summary>
    /// 创建配置的副本
    /// </summary>
    public TcpClientConfig Clone() => new()
    {
        Host = Host,
        Port = Port,
        ConnectTimeout = ConnectTimeout,
        ReceiveTimeout = ReceiveTimeout,
        SendTimeout = SendTimeout,
        KeepAlive = KeepAlive,
        NoDelay = NoDelay
    };
}

/// <summary>
/// TCP 服务器配置
/// </summary>
public class TcpServerConfig : NetworkConfig
{
    /// <inheritdoc/>
    public override ConnectionType ConnectionType => ConnectionType.TcpServer;

    /// <summary>
    /// 监听地址 (默认监听所有网卡)
    /// </summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// 监听端口
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 最大连接数
    /// </summary>
    public int MaxConnections { get; set; } = 10;

    /// <summary>
    /// 等待连接队列长度
    /// </summary>
    public int Backlog { get; set; } = 100;

    /// <summary>
    /// 创建配置的副本
    /// </summary>
    public TcpServerConfig Clone() => new()
    {
        ListenAddress = ListenAddress,
        Port = Port,
        MaxConnections = MaxConnections,
        Backlog = Backlog
    };
}

/// <summary>
/// UDP 配置
/// </summary>
public class UdpConfig : NetworkConfig
{
    /// <inheritdoc/>
    public override ConnectionType ConnectionType => ConnectionType.Udp;

    /// <summary>
    /// 本地绑定端口 (0 表示自动分配)
    /// </summary>
    public int LocalPort { get; set; } = 0;

    /// <summary>
    /// 默认远程主机地址 (用于发送)
    /// </summary>
    public string RemoteHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// 默认远程端口 (用于发送)
    /// </summary>
    public int RemotePort { get; set; } = 8080;

    /// <summary>
    /// 是否启用广播
    /// </summary>
    public bool EnableBroadcast { get; set; } = false;

    /// <summary>
    /// 接收缓冲区大小
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 65536;

    /// <summary>
    /// 创建配置的副本
    /// </summary>
    public UdpConfig Clone() => new()
    {
        LocalPort = LocalPort,
        RemoteHost = RemoteHost,
        RemotePort = RemotePort,
        EnableBroadcast = EnableBroadcast,
        ReceiveBufferSize = ReceiveBufferSize
    };
}
