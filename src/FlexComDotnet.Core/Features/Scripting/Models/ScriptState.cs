namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// 脚本运行状态
/// </summary>
public enum ScriptState
{
    /// <summary>
    /// 空闲（未加载或已停止）
    /// </summary>
    Idle,

    /// <summary>
    /// 正在运行
    /// </summary>
    Running,

    /// <summary>
    /// 已暂停
    /// </summary>
    Paused,

    /// <summary>
    /// 运行出错
    /// </summary>
    Error,

    /// <summary>
    /// 正在停止中
    /// </summary>
    Stopping
}
