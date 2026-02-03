namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 匹配规则模型
/// </summary>
public class MatchRule
{
    /// <summary>
    /// 规则唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 规则名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 触发条件（Hex 或 ASCII 字符串）
    /// </summary>
    public string TriggerPattern { get; set; } = string.Empty;

    /// <summary>
    /// 匹配类型
    /// </summary>
    public MatchType MatchType { get; set; } = MatchType.HexContains;

    /// <summary>
    /// 响应内容（Hex 或 ASCII 字符串）
    /// </summary>
    public string ResponseContent { get; set; } = string.Empty;

    /// <summary>
    /// 响应内容是否为 Hex 格式
    /// </summary>
    public bool IsResponseHex { get; set; } = true;

    /// <summary>
    /// 是否启用此规则
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 规则描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
