namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 多帧联合决策模式
/// </summary>
public enum DecisionMode
{
    /// <summary>全条件交集：窗口内所有帧必须全部匹配</summary>
    AND,
    /// <summary>任意条件并集：窗口内任意一帧匹配即触发</summary>
    OR,
    /// <summary>末帧判定：仅对窗口内最后一帧做规则校验</summary>
    LAST,
    /// <summary>首帧判定：仅对窗口内第一帧做规则校验</summary>
    FIRST
}
