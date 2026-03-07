using FlexComDotnet.Core.Features.Logging.Models;

namespace FlexComDotnet.Core.Features.Logging.Services;

/// <summary>
/// 日志持久化服务接口
/// </summary>
public interface ILogPersistenceService : IDisposable
{
    /// <summary>
    /// 写入日志条目到文件
    /// </summary>
    void Write(LogEntry entry);

    /// <summary>
    /// 写入会话开始标记
    /// </summary>
    void WriteSessionStart();

    /// <summary>
    /// 写入会话结束标记
    /// </summary>
    void WriteSessionEnd();

    /// <summary>
    /// 刷新缓冲区
    /// </summary>
    void Flush();
}
