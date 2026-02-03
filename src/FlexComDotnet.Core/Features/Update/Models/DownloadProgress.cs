namespace FlexComDotnet.Core.Features.Update.Models;

/// <summary>
/// 下载进度信息
/// </summary>
public record DownloadProgress
{
    /// <summary>
    /// 已下载字节数
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// 总字节数 (如果已知)
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// 下载进度百分比 (0-100)
    /// </summary>
    public double ProgressPercentage => TotalBytes > 0
        ? (double)BytesReceived / TotalBytes.Value * 100
        : 0;

    /// <summary>
    /// 下载速度 (字节/秒)
    /// </summary>
    public long BytesPerSecond { get; init; }

    /// <summary>
    /// 预计剩余时间
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (TotalBytes is null || BytesPerSecond <= 0)
                return null;

            var remainingBytes = TotalBytes.Value - BytesReceived;
            return TimeSpan.FromSeconds((double)remainingBytes / BytesPerSecond);
        }
    }

    /// <summary>
    /// 格式化的已下载大小
    /// </summary>
    public string FormattedBytesReceived => FormatBytes(BytesReceived);

    /// <summary>
    /// 格式化的总大小
    /// </summary>
    public string? FormattedTotalBytes => TotalBytes.HasValue ? FormatBytes(TotalBytes.Value) : null;

    /// <summary>
    /// 格式化的下载速度
    /// </summary>
    public string FormattedSpeed => $"{FormatBytes(BytesPerSecond)}/s";

    /// <summary>
    /// 将字节数格式化为易读字符串
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// 下载状态
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// 空闲
    /// </summary>
    Idle,

    /// <summary>
    /// 下载中
    /// </summary>
    Downloading,

    /// <summary>
    /// 已暂停
    /// </summary>
    Paused,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}
