namespace FlexComDotnet.Core.Features.Visualization.Models;

/// <summary>
/// 数据点新增事件参数
/// </summary>
public class DataPointAddedEventArgs : EventArgs
{
    /// <summary>
    /// 新增的数据点
    /// </summary>
    public ChartDataPoint DataPoint { get; }

    public DataPointAddedEventArgs(ChartDataPoint dataPoint)
    {
        DataPoint = dataPoint;
    }
}

/// <summary>
/// 可视化状态变更事件参数
/// </summary>
public class VisualizationStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning { get; }

    public VisualizationStateChangedEventArgs(bool isRunning)
    {
        IsRunning = isRunning;
    }
}

/// <summary>
/// 帧提取失败事件参数
/// </summary>
public class ExtractionFailedEventArgs : EventArgs
{
    /// <summary>
    /// 已接收但无法提取帧的字节数
    /// </summary>
    public long BytesReceived { get; }

    /// <summary>
    /// 已成功提取的帧数
    /// </summary>
    public long FramesExtracted { get; }

    public ExtractionFailedEventArgs(long bytesReceived, long framesExtracted)
    {
        BytesReceived = bytesReceived;
        FramesExtracted = framesExtracted;
    }
}
