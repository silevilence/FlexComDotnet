namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 自动回复全局配置
/// </summary>
public class AutoReplyConfig
{
    /// <summary>
    /// 是否启用自动回复
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 全局回复延迟（毫秒）
    /// </summary>
    public int GlobalDelayMs { get; set; } = 100;

    /// <summary>
    /// 当前激活的回复模式
    /// </summary>
    public ReplyMode ActiveMode { get; set; } = ReplyMode.Match;

    /// <summary>
    /// 匹配回复配置
    /// </summary>
    public MatchReplyConfig MatchConfig { get; set; } = new();

    /// <summary>
    /// 顺序回复配置
    /// </summary>
    public SequentialReplyConfig SequentialConfig { get; set; } = new();
}
