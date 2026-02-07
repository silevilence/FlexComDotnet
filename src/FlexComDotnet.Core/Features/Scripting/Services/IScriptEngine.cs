using FlexComDotnet.Core.Features.Scripting.Models;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本引擎接口 - 负责 Lua 脚本的加载、执行和停止
/// </summary>
public interface IScriptEngine : IDisposable
{
    /// <summary>
    /// 当前脚本运行状态
    /// </summary>
    ScriptState State { get; }

    /// <summary>
    /// 当前加载的脚本名称
    /// </summary>
    string? CurrentScriptName { get; }

    /// <summary>
    /// 脚本状态变更事件
    /// </summary>
    event EventHandler<ScriptState>? StateChanged;

    /// <summary>
    /// 脚本日志输出事件
    /// </summary>
    event EventHandler<ScriptLogEntry>? LogOutput;

    /// <summary>
    /// 脚本执行出错事件
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// 执行 Lua 脚本代码
    /// </summary>
    /// <param name="scriptCode">Lua 脚本代码</param>
    /// <param name="scriptName">脚本名称（用于日志标识）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<ScriptExecutionResult> ExecuteAsync(string scriptCode, string scriptName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止当前正在执行的脚本
    /// </summary>
    void Stop();

    /// <summary>
    /// 注册 API 桥接对象
    /// </summary>
    /// <param name="bridge">API 桥接实例</param>
    void RegisterApiBridge(IScriptApiBridge bridge);
}
