namespace FlexComDotnet.Core.Features.Visualization.Models;

/// <summary>
/// 图表数据点
/// </summary>
public class ChartDataPoint
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 数据值
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 所属通道 ID
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    public ChartDataPoint()
    {
    }

    public ChartDataPoint(string channelId, double value, DateTime timestamp)
    {
        ChannelId = channelId;
        Value = value;
        Timestamp = timestamp;
    }
}
