namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// Hook 执行结果
/// </summary>
public class HookExecutionResult
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 处理后的数据（用于 Pipeline Hook）
    /// </summary>
    public byte[]? ProcessedData { get; init; }

    /// <summary>
    /// 是否应该回复（用于 Reply Hook）
    /// </summary>
    public bool ShouldReply { get; init; }

    /// <summary>
    /// 回复数据（用于 Reply Hook）
    /// </summary>
    public byte[]? ReplyData { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// 创建成功结果（Pipeline Hook）
    /// </summary>
    public static HookExecutionResult SuccessWithData(byte[] data, long elapsedMs = 0)
        => new() { Success = true, ProcessedData = data, ElapsedMs = elapsedMs };

    /// <summary>
    /// 创建成功结果（Reply Hook - 需要回复）
    /// </summary>
    public static HookExecutionResult SuccessWithReply(byte[] replyData, long elapsedMs = 0)
        => new() { Success = true, ShouldReply = true, ReplyData = replyData, ElapsedMs = elapsedMs };

    /// <summary>
    /// 创建成功结果（Reply Hook - 不需要回复）
    /// </summary>
    public static HookExecutionResult SuccessNoReply(long elapsedMs = 0)
        => new() { Success = true, ShouldReply = false, ElapsedMs = elapsedMs };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static HookExecutionResult Failed(string errorMessage, long elapsedMs = 0)
        => new() { Success = false, ErrorMessage = errorMessage, ElapsedMs = elapsedMs };

    /// <summary>
    /// 创建跳过结果（Hook 未启用或无脚本）
    /// </summary>
    public static HookExecutionResult Skipped()
        => new() { Success = true };
}
