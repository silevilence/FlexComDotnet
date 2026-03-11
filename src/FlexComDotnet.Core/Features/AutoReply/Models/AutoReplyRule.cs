namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 统一自动回复规则 - 所有类型的规则共用一个模型
/// </summary>
public class AutoReplyRule
{
    /// <summary>
    /// 规则唯一标识
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 规则名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 规则描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 规则类型
    /// </summary>
    public ReplyMode Type { get; set; } = ReplyMode.Match;

    /// <summary>
    /// 是否启用此规则（支持多选激活）
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 优先级排序（数字越小优先级越高）
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 匹配回复配置（Type == Match 时有效）
    /// </summary>
    public MatchRuleConfig? MatchConfig { get; set; }

    /// <summary>
    /// 顺序回复配置（Type == Sequential 时有效）
    /// </summary>
    public SequentialRuleConfig? SequentialConfig { get; set; }

    /// <summary>
    /// 协议回复配置（Type == Protocol 时有效）
    /// </summary>
    public ProtocolRuleConfig? ProtocolConfig { get; set; }
}

/// <summary>
/// 匹配规则配置（内嵌于 AutoReplyRule）
/// </summary>
public class MatchRuleConfig
{
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
}

/// <summary>
/// 顺序回复规则配置（内嵌于 AutoReplyRule）
/// 每个规则实例拥有独立的帧组和循环设置
/// </summary>
public class SequentialRuleConfig
{
    /// <summary>
    /// 预设帧列表
    /// </summary>
    public List<SequentialFrame> Frames { get; set; } = [];

    /// <summary>
    /// 是否循环回复
    /// </summary>
    public bool EnableLoop { get; set; } = true;

    /// <summary>
    /// 当前回复索引（运行时状态，不持久化）
    /// </summary>
    public int CurrentIndex { get; set; }
}

/// <summary>
/// 协议回复规则配置（内嵌于 AutoReplyRule）
/// </summary>
public class ProtocolRuleConfig
{
    /// <summary>
    /// 关联的协议名称
    /// </summary>
    public string ProtocolName { get; set; } = string.Empty;

    /// <summary>
    /// 字段值配置（字段名 -> 值表达式）
    /// </summary>
    public Dictionary<string, string> FieldValues { get; set; } = [];
}
