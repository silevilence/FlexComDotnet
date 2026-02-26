using FlexComDotnet.Core.Features.Scripting.Models;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本自动应答事件参数
/// </summary>
public class ScriptAutoReplyEventArgs : EventArgs
{
    /// <summary>
    /// 脚本决定回复的原始数据
    /// </summary>
    public byte[] ReplyData { get; }
    
    /// <summary>
    /// 经过 Tx Hook 处理后实际发送的数据
    /// </summary>
    public byte[] ProcessedReplyData { get; }

    public ScriptAutoReplyEventArgs(byte[] replyData, byte[] processedReplyData)
    {
        ReplyData = replyData;
        ProcessedReplyData = processedReplyData;
    }
}

/// <summary>
/// 脚本 Hook 服务接口 - 管理数据流钩子和应答钩子
/// </summary>
public interface IScriptHookService
{
    /// <summary>
    /// Hook 配置
    /// </summary>
    ScriptHookSettings Settings { get; }

    /// <summary>
    /// Hook 执行日志事件
    /// </summary>
    event EventHandler<ScriptLogEntry>? LogOutput;

    /// <summary>
    /// 脚本自动应答事件
    /// </summary>
    event EventHandler<ScriptAutoReplyEventArgs>? AutoReplySent;

    /// <summary>
    /// 更新 Hook 配置
    /// </summary>
    void UpdateSettings(ScriptHookSettings settings);

    /// <summary>
    /// 设置指定类型的 Hook 脚本
    /// </summary>
    /// <param name="hookType">Hook 类型</param>
    /// <param name="scriptId">脚本 ID（null 表示清除）</param>
    void SetHookScript(HookType hookType, string? scriptId);

    /// <summary>
    /// 启用/禁用指定类型的 Hook
    /// </summary>
    /// <param name="hookType">Hook 类型</param>
    /// <param name="enabled">是否启用</param>
    void SetHookEnabled(HookType hookType, bool enabled);

    /// <summary>
    /// 执行接收预处理 Hook
    /// </summary>
    /// <param name="data">原始接收数据</param>
    /// <returns>处理结果（包含处理后的数据）</returns>
    Task<HookExecutionResult> ExecuteRxPreProcessorAsync(byte[] data);

    /// <summary>
    /// 执行发送后处理 Hook
    /// </summary>
    /// <param name="data">原始发送数据</param>
    /// <returns>处理结果（包含处理后的数据）</returns>
    Task<HookExecutionResult> ExecuteTxPostProcessorAsync(byte[] data);

    /// <summary>
    /// 执行应答 Hook
    /// </summary>
    /// <param name="receivedData">接收到的数据</param>
    /// <returns>处理结果（包含是否回复及回复数据）</returns>
    Task<HookExecutionResult> ExecuteReplyHookAsync(byte[] receivedData);

    /// <summary>
    /// 执行任务 Hook（手动触发）
    /// </summary>
    /// <param name="scriptId">脚本 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<ScriptExecutionResult> ExecuteTaskAsync(string scriptId, CancellationToken cancellationToken = default);
}
