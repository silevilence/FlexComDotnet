namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 协议回复配置
/// </summary>
public class ProtocolReplyConfig
{
    /// <summary>
    /// 配置方案列表
    /// </summary>
    public List<ProtocolReplyScheme> Schemes { get; set; } = [];

    /// <summary>
    /// 当前激活的方案索引（-1 表示无激活方案）
    /// </summary>
    public int ActiveSchemeIndex { get; set; } = -1;
}

/// <summary>
/// 协议回复配置方案
/// </summary>
public class ProtocolReplyScheme
{
    /// <summary>
    /// 方案唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 方案名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 方案描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 关联的协议名称
    /// </summary>
    public string ProtocolName { get; set; } = string.Empty;

    /// <summary>
    /// 字段值配置（字段名 -> 值表达式）
    /// 支持字符串插值语法，如 "{receivedValue + 1}"
    /// </summary>
    public Dictionary<string, string> FieldValues { get; set; } = [];

    /// <summary>
    /// 是否启用此方案
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }
}
