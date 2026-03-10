namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 自动回复模式枚举
/// </summary>
public enum ReplyMode
{
    /// <summary>
    /// 匹配回复模式 - 检测特定特征码触发回复
    /// </summary>
    Match = 0,

    /// <summary>
    /// 顺序回复模式 - 按预设顺序依次回复
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// 脚本回复模式 - 使用 Lua 脚本处理复杂应答逻辑
    /// </summary>
    Script = 2,

    /// <summary>
    /// 协议回复模式 - 根据协议定义动态构建回复帧
    /// </summary>
    Protocol = 3
}
