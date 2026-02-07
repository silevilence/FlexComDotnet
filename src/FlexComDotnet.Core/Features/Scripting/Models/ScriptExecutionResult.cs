namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// 脚本执行结果
/// </summary>
public class ScriptExecutionResult
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 错误消息（执行失败时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ScriptExecutionResult Succeeded(long elapsedMs = 0) =>
        new() { Success = true, ElapsedMs = elapsedMs };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ScriptExecutionResult Failed(string errorMessage, long elapsedMs = 0) =>
        new() { Success = false, ErrorMessage = errorMessage, ElapsedMs = elapsedMs };
}
