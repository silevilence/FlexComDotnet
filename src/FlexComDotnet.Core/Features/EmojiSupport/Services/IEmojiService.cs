using FlexComDotnet.Core.Features.EmojiSupport.Models;

namespace FlexComDotnet.Core.Features.EmojiSupport.Services;

/// <summary>
/// Emoji 服务接口 - 提供短码查询和搜索功能
/// </summary>
public interface IEmojiService
{
    /// <summary>
    /// 根据前缀搜索 Emoji（不含冒号）
    /// </summary>
    IReadOnlyList<EmojiEntry> Search(string prefix, int maxResults = 10);

    /// <summary>
    /// 根据完整短码获取 Emoji
    /// </summary>
    EmojiEntry? GetByShortcode(string shortcode);

    /// <summary>
    /// 获取所有 Emoji 条目
    /// </summary>
    IReadOnlyList<EmojiEntry> GetAll();
}
