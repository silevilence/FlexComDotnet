namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 回复处理结果
/// </summary>
public class ReplyResult
{
    /// <summary>
    /// 是否应该回复
    /// </summary>
    public bool ShouldReply { get; init; }

    /// <summary>
    /// 回复数据（字节数组）
    /// </summary>
    public byte[] ResponseData { get; init; } = [];

    /// <summary>
    /// 匹配的规则名称（用于日志）
    /// </summary>
    public string? MatchedRuleName { get; init; }

    /// <summary>
    /// 创建一个不回复的结果
    /// </summary>
    public static ReplyResult NoReply => new() { ShouldReply = false };

    /// <summary>
    /// 创建一个回复结果
    /// </summary>
    public static ReplyResult Reply(byte[] data, string? ruleName = null) => new()
    {
        ShouldReply = true,
        ResponseData = data,
        MatchedRuleName = ruleName
    };
}
