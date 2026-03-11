namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// Hook 配置
/// </summary>
public class HookConfig
{
    /// <summary>
    /// Hook 类型
    /// </summary>
    public HookType Type { get; set; }

    /// <summary>
    /// 关联的脚本 ID
    /// </summary>
    public string? ScriptId { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Hook 显示名称
    /// </summary>
    public string DisplayName => Type switch
    {
        HookType.RxPreProcessor => "接收预处理",
        HookType.TxPostProcessor => "发送后处理",
        HookType.Reply => "脚本应答",
        HookType.Task => "自动化任务",
        _ => "未知"
    };
}

/// <summary>
/// 全局 Hook 配置
/// </summary>
public class ScriptHookSettings
{
    /// <summary>
    /// 接收预处理器配置
    /// </summary>
    public HookConfig RxPreProcessor { get; set; } = new() { Type = HookType.RxPreProcessor };

    /// <summary>
    /// 发送后处理器配置
    /// </summary>
    public HookConfig TxPostProcessor { get; set; } = new() { Type = HookType.TxPostProcessor };

    /// <summary>
    /// 应答钩子配置
    /// </summary>
    public HookConfig Reply { get; set; } = new() { Type = HookType.Reply };
}

/// <summary>
/// 持久化用 Hook 配置（仅保存脚本绑定，不保存启用状态）
/// </summary>
public class ScriptHookConfig
{
    /// <summary>
    /// 接收预处理 Hook 绑定的脚本 ID
    /// </summary>
    public string? RxPreProcessorScriptId { get; set; }

    /// <summary>
    /// 发送后处理 Hook 绑定的脚本 ID
    /// </summary>
    public string? TxPostProcessorScriptId { get; set; }

    /// <summary>
    /// 应答 Hook 绑定的脚本 ID
    /// </summary>
    public string? ReplyScriptId { get; set; }
}
