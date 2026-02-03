using System.Net;

namespace FlexComDotnet.Core.Features.Network.Models;

/// <summary>
/// 数据接收事件参数
/// </summary>
public class DataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 接收到的数据
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// 远程端点 (对于 TCP Server 和 UDP, 指示数据来源)
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedTime { get; }

    public DataReceivedEventArgs(byte[] data, IPEndPoint? remoteEndPoint = null)
    {
        Data = data;
        RemoteEndPoint = remoteEndPoint;
        ReceivedTime = DateTime.Now;
    }
}
