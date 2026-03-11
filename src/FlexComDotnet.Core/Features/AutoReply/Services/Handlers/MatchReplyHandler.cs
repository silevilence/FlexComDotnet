using System.Text;
using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.AutoReply.Services.Handlers;

/// <summary>
/// 匹配回复处理器 - 检测特定特征码触发回复
/// </summary>
public class MatchReplyHandler : IReplyHandler
{
    /// <inheritdoc/>
    public ReplyMode Mode => ReplyMode.Match;

    /// <inheritdoc/>
    public string DisplayName => "匹配回复";

    /// <inheritdoc/>
    public string Description => "检测接收数据中的特定特征码，匹配成功后自动发送预设响应";

    /// <inheritdoc/>
    public ReplyResult Process(byte[] receivedData, AutoReplyRule rule)
    {
        if (receivedData.Length == 0 || rule.MatchConfig == null)
        {
            return ReplyResult.NoReply;
        }

        var matchConfig = rule.MatchConfig;

        if (string.IsNullOrEmpty(matchConfig.TriggerPattern))
        {
            return ReplyResult.NoReply;
        }

        if (IsMatch(receivedData, matchConfig))
        {
            var responseData = GetResponseData(matchConfig);
            if (responseData.Length > 0)
            {
                return ReplyResult.Reply(responseData, rule.Name);
            }
        }

        return ReplyResult.NoReply;
    }

    /// <inheritdoc/>
    public void Reset(AutoReplyRule rule)
    {
        // 匹配模式无状态需要重置
    }

    /// <summary>
    /// 检查接收数据是否匹配规则
    /// </summary>
    private static bool IsMatch(byte[] receivedData, MatchRuleConfig config)
    {
        return config.MatchType switch
        {
            Models.MatchType.HexContains => IsHexContainsMatch(receivedData, config.TriggerPattern),
            Models.MatchType.HexExact => IsHexExactMatch(receivedData, config.TriggerPattern),
            Models.MatchType.AsciiContains => IsAsciiContainsMatch(receivedData, config.TriggerPattern),
            Models.MatchType.AsciiExact => IsAsciiExactMatch(receivedData, config.TriggerPattern),
            _ => false
        };
    }

    /// <summary>
    /// 十六进制包含匹配
    /// </summary>
    private static bool IsHexContainsMatch(byte[] receivedData, string pattern)
    {
        var patternBytes = HexHelper.HexStringToBytes(pattern);
        if (patternBytes.Length == 0)
        {
            return false;
        }

        return ContainsSubsequence(receivedData, patternBytes);
    }

    /// <summary>
    /// 十六进制完全匹配
    /// </summary>
    private static bool IsHexExactMatch(byte[] receivedData, string pattern)
    {
        var patternBytes = HexHelper.HexStringToBytes(pattern);
        if (patternBytes.Length == 0)
        {
            return false;
        }

        return receivedData.SequenceEqual(patternBytes);
    }

    /// <summary>
    /// ASCII 包含匹配
    /// </summary>
    private static bool IsAsciiContainsMatch(byte[] receivedData, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var receivedString = Encoding.ASCII.GetString(receivedData);
        return receivedString.Contains(pattern, StringComparison.Ordinal);
    }

    /// <summary>
    /// ASCII 完全匹配
    /// </summary>
    private static bool IsAsciiExactMatch(byte[] receivedData, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var receivedString = Encoding.ASCII.GetString(receivedData);
        return receivedString.Equals(pattern, StringComparison.Ordinal);
    }

    /// <summary>
    /// 检查字节数组是否包含子序列
    /// </summary>
    private static bool ContainsSubsequence(byte[] data, byte[] subsequence)
    {
        if (subsequence.Length > data.Length)
        {
            return false;
        }

        for (int i = 0; i <= data.Length - subsequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < subsequence.Length; j++)
            {
                if (data[i + j] != subsequence[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取响应数据
    /// </summary>
    private static byte[] GetResponseData(MatchRuleConfig config)
    {
        if (string.IsNullOrEmpty(config.ResponseContent))
        {
            return [];
        }

        if (config.IsResponseHex)
        {
            return HexHelper.HexStringToBytes(config.ResponseContent);
        }
        else
        {
            return Encoding.ASCII.GetBytes(config.ResponseContent);
        }
    }
}
