using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// Hook 处理事件参数
/// </summary>
public class HookProcessedEventArgs : EventArgs
{
    public byte[] OriginalData { get; }
    public byte[] ProcessedData { get; }
    public bool IsTx { get; }

    public HookProcessedEventArgs(byte[] originalData, byte[] processedData, bool isTx)
    {
        OriginalData = originalData;
        ProcessedData = processedData;
        IsTx = isTx;
    }
}

/// <summary>
/// 串口服务接口
/// </summary>
public interface ISerialPortService
{
    /// <summary>
    /// 当前连接状态
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 当前使用的配置
    /// </summary>
    SerialPortConfig? CurrentConfig { get; }

    /// <summary>
    /// 数据接收事件
    /// </summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// 错误事件
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Hook 处理完成事件（数据被修改时触发）
    /// </summary>
    event EventHandler<HookProcessedEventArgs>? HookProcessed;

    /// <summary>
    /// 接收数据预处理器 (Rx Hook)
    /// </summary>
    Func<byte[], byte[]>? RxPreProcessor { get; set; }

    /// <summary>
    /// 发送数据后处理器 (Tx Hook)
    /// </summary>
    Func<byte[], byte[]>? TxPostProcessor { get; set; }

    /// <summary>
    /// 获取可用串口列表
    /// </summary>
    IReadOnlyList<SerialPortInfo> GetAvailablePorts();

    /// <summary>
    /// 打开串口
    /// </summary>
    /// <param name="config">串口配置</param>
    /// <returns>是否成功</returns>
    bool Open(SerialPortConfig config);

    /// <summary>
    /// 关闭串口
    /// </summary>
    void Close();

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <returns>是否成功</returns>
    bool Send(byte[] data);

    /// <summary>
    /// 发送字符串 (使用UTF-8编码)
    /// </summary>
    /// <param name="text">要发送的文本</param>
    /// <returns>是否成功</returns>
    bool Send(string text);
}
