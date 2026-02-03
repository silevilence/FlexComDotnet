namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 匹配回复配置
/// </summary>
public class MatchReplyConfig
{
    /// <summary>
    /// 匹配规则列表
    /// </summary>
    public List<MatchRule> Rules { get; set; } = [];
}
