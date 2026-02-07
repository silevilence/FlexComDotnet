namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// 脚本日志条目
/// </summary>
public class ScriptLogEntry
{
    /// <summary>
    /// 日志时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// 日志级别
    /// </summary>
    public ScriptLogLevel Level { get; init; } = ScriptLogLevel.Info;

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 脚本名称（产生日志的脚本）
    /// </summary>
    public string ScriptName { get; init; } = string.Empty;
}
