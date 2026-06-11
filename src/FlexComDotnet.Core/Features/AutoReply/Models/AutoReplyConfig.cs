using System.Text.Json.Serialization;

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
    /// 防抖窗口（毫秒）。替代原 GlobalDelayMs。≥1
    /// </summary>
    public int DebounceWindowMs { get; set; } = 50;

    /// <summary>
    /// 旧版配置兼容 — 从 GlobalDelayMs 迁移到 DebounceWindowMs
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int GlobalDelayMs
    {
        get => DebounceWindowMs;
        set => DebounceWindowMs = value > 0 ? value : 50;
    }

    /// <summary>
    /// 多帧联合决策模式，默认末帧判定
    /// </summary>
    public DecisionMode DecisionMode { get; set; } = DecisionMode.LAST;

    /// <summary>
    /// 统一规则池 - 所有类型的规则均在此列表中管理
    /// 支持多选激活，按 SortOrder 优先级顺序执行
    /// </summary>
    public List<AutoReplyRule> Rules { get; set; } = [];
}
