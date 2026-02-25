namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// 脚本 Hook 类型枚举
/// </summary>
public enum HookType
{
    /// <summary>
    /// 接收预处理器 - 修改/解密接收数据
    /// </summary>
    RxPreProcessor = 0,

    /// <summary>
    /// 发送后处理器 - 加封包/校验发送数据
    /// </summary>
    TxPostProcessor = 1,

    /// <summary>
    /// 应答钩子 - 脚本模式自动回复
    /// </summary>
    Reply = 2,

    /// <summary>
    /// 任务钩子 - 手动触发的自动化任务
    /// </summary>
    Task = 3
}
