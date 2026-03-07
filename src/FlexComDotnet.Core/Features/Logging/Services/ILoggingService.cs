using FlexComDotnet.Core.Features.Logging.Models;

namespace FlexComDotnet.Core.Features.Logging.Services;

/// <summary>
/// 统一日志服务接口
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// 记录日志
    /// </summary>
    void Log(LogLevel level, LogSource source, string message);

    /// <summary>
    /// 记录信息日志
    /// </summary>
    void Info(LogSource source, string message);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    void Warning(LogSource source, string message);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    void Error(LogSource source, string message);

    /// <summary>
    /// 记录调试日志
    /// </summary>
    void Debug(LogSource source, string message);

    /// <summary>
    /// 获取当前会话的所有日志条目
    /// </summary>
    IReadOnlyList<LogEntry> Entries { get; }

    /// <summary>
    /// 新日志条目事件
    /// </summary>
    event EventHandler<LogEntry> LogAdded;
}
