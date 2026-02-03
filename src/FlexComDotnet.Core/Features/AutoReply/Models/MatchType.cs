namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 匹配类型枚举
/// </summary>
public enum MatchType
{
    /// <summary>
    /// 十六进制包含匹配
    /// </summary>
    HexContains = 0,

    /// <summary>
    /// ASCII 文本包含匹配
    /// </summary>
    AsciiContains = 1,

    /// <summary>
    /// 十六进制完全匹配
    /// </summary>
    HexExact = 2,

    /// <summary>
    /// ASCII 文本完全匹配
    /// </summary>
    AsciiExact = 3
}
