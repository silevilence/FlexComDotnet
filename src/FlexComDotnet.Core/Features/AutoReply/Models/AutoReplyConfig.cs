namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 自动回复全局配置
/// </summary>
public class AutoReplyConfig
{
    /// <summary>
    /// 是否启用自动回复（全局开关）
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 全局回复延迟（毫秒）
    /// </summary>
    public int GlobalDelayMs { get; set; } = 100;

    /// <summary>
    /// 统一规则池 - 所有类型的规则均在此列表中管理
    /// 支持多选激活，按 SortOrder 优先级顺序执行
    /// </summary>
    public List<AutoReplyRule> Rules { get; set; } = [];
}
