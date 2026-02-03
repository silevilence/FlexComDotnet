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
    Sequential = 1
}
