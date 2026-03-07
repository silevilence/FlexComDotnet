namespace FlexComDotnet.Core.Features.Logging.Models;

/// <summary>
/// 统一日志条目
/// </summary>
public class LogEntry
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// 日志等级
    /// </summary>
    public LogLevel Level { get; init; } = LogLevel.Info;

    /// <summary>
    /// 来源模块
    /// </summary>
    public LogSource Source { get; init; } = LogSource.System;

    /// <summary>
    /// 日志内容
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
