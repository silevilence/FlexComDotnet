namespace FlexComDotnet.Core.Features.EmojiSupport.Models;

/// <summary>
/// 单个 Emoji 条目
/// </summary>
public class EmojiEntry
{
    /// <summary>
    /// Emoji 字符（Unicode）
    /// </summary>
    public string Emoji { get; init; } = string.Empty;

    /// <summary>
    /// 短码（不含冒号），如 "smile"
    /// </summary>
    public string Shortcode { get; init; } = string.Empty;

    /// <summary>
    /// 分类
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 完整短码格式 ":smile:"
    /// </summary>
    public string FullShortcode => $":{Shortcode}:";
}
