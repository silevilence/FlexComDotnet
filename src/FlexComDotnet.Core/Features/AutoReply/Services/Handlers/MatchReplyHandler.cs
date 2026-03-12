using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.AutoReply.Services.Handlers;

/// <summary>
/// 匹配回复处理器 - 检测特定特征码触发回复
/// </summary>
public class MatchReplyHandler : IReplyHandler
{
    private readonly IProtocolParserService? _protocolParserService;

    /// <inheritdoc/>
    public ReplyMode Mode => ReplyMode.Match;

    /// <inheritdoc/>
    public string DisplayName => "匹配回复";

    /// <inheritdoc/>
    public string Description => "检测接收数据中的特定特征码，匹配成功后自动发送预设响应";

    public MatchReplyHandler(IProtocolParserService? protocolParserService = null)
    {
        _protocolParserService = protocolParserService;
    }

    /// <inheritdoc/>
    public ReplyResult Process(byte[] receivedData, AutoReplyRule rule)
    {
        if (receivedData.Length == 0 || rule.MatchConfig == null)
        {
            return ReplyResult.NoReply;
        }

        var matchConfig = rule.MatchConfig;

        // 协议级触发条件
        ParsedFrame? parsedFrame = null;
        if (matchConfig.MatchType == Models.MatchType.ProtocolParse)
        {
            parsedFrame = TryProtocolMatch(receivedData, matchConfig);
            if (parsedFrame == null)
                return ReplyResult.NoReply;
        }
        else
        {
            if (string.IsNullOrEmpty(matchConfig.TriggerPattern))
                return ReplyResult.NoReply;

            if (!IsMatch(receivedData, matchConfig))
                return ReplyResult.NoReply;
        }

        var responseData = GetResponseData(matchConfig, receivedData, parsedFrame);
        if (responseData.Length > 0)
        {
            return ReplyResult.Reply(responseData, rule.Name);
        }

        return ReplyResult.NoReply;
    }

    /// <inheritdoc/>
    public void Reset(AutoReplyRule rule)
    {
        // 匹配模式无状态需要重置
    }

    /// <summary>
    /// 尝试协议级匹配（解析成功 + 字段断言）
    /// </summary>
    private ParsedFrame? TryProtocolMatch(byte[] receivedData, MatchRuleConfig config)
    {
        if (_protocolParserService == null || string.IsNullOrEmpty(config.TriggerProtocolName))
            return null;

        var parser = _protocolParserService.GetParser(config.TriggerProtocolName);
        if (parser == null)
            return null;

        try
        {
            if (!parser.Validate(receivedData))
                return null;

            var parsed = parser.Parse(receivedData);
            if (!parsed.IsValid)
                return null;

            // 检查字段断言（AND 逻辑）
            if (config.FieldAssertions.Count > 0)
            {
                foreach (var assertion in config.FieldAssertions)
                {
                    if (!EvaluateAssertion(parsed, assertion))
                        return null;
                }
            }

            return parsed;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 评估单个字段断言
    /// </summary>
    private static bool EvaluateAssertion(ParsedFrame parsed, FieldAssertion assertion)
    {
        var field = parsed.GetField(assertion.FieldName);
        if (field == null)
            return false;

        var fieldValue = field.Value;
        if (fieldValue == null)
            return false;

        if (assertion.Operator == AssertionOperator.HexContains)
        {
            var fieldHex = field.HexValue.Replace(" ", "");
            var expectedHex = assertion.ExpectedValue.Replace(" ", "");
            return fieldHex.Contains(expectedHex, StringComparison.OrdinalIgnoreCase);
        }

        // 尝试数值比较
        if (double.TryParse(fieldValue.ToString(), CultureInfo.InvariantCulture, out var numericFieldValue)
            && double.TryParse(assertion.ExpectedValue, CultureInfo.InvariantCulture, out var numericExpected))
        {
            return assertion.Operator switch
            {
                AssertionOperator.Equal => Math.Abs(numericFieldValue - numericExpected) < 0.0001,
                AssertionOperator.GreaterThan => numericFieldValue > numericExpected,
                AssertionOperator.GreaterThanOrEqual => numericFieldValue >= numericExpected,
                AssertionOperator.LessThan => numericFieldValue < numericExpected,
                AssertionOperator.LessThanOrEqual => numericFieldValue <= numericExpected,
                _ => false
            };
        }

        // 字符串比较（Equal 适用）
        if (assertion.Operator == AssertionOperator.Equal)
        {
            return string.Equals(fieldValue.ToString(), assertion.ExpectedValue, StringComparison.OrdinalIgnoreCase);
        }

        return false;
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
    /// 获取响应数据 - 支持纯文本和协议组帧两种模式
    /// </summary>
    private byte[] GetResponseData(MatchRuleConfig config, byte[] receivedData, ParsedFrame? parsedFrame)
    {
        if (config.ResponseMode == ResponseBuildMode.ProtocolBuild)
        {
            return BuildProtocolResponse(config.ProtocolResponse, receivedData, parsedFrame);
        }

        // 纯文本模式
        if (string.IsNullOrEmpty(config.ResponseContent))
            return [];

        // 支持 {} 插值
        var content = InterpolateExpression(config.ResponseContent, parsedFrame);

        if (config.IsResponseHex)
        {
            return HexHelper.HexStringToBytes(content);
        }
        else
        {
            return Encoding.ASCII.GetBytes(content);
        }
    }

    /// <summary>
    /// 使用协议动态构建响应帧
    /// </summary>
    private byte[] BuildProtocolResponse(ProtocolResponseConfig? responseConfig, byte[] receivedData, ParsedFrame? parsedFrame)
    {
        if (responseConfig == null || _protocolParserService == null || string.IsNullOrEmpty(responseConfig.ProtocolName))
            return [];

        var parser = _protocolParserService.GetParser(responseConfig.ProtocolName);
        if (parser == null)
            return [];

        try
        {
            var fieldValues = new Dictionary<string, object>();
            foreach (var (fieldName, expression) in responseConfig.FieldValues)
            {
                if (string.IsNullOrEmpty(expression))
                    continue;

                var evaluated = InterpolateExpression(expression, parsedFrame);
                // Hex 模式：将 hex 字符串转换为 byte[]
                if (responseConfig.FieldHexModes.TryGetValue(fieldName, out var isHex) && isHex)
                {
                    fieldValues[fieldName] = Serial.Helpers.HexHelper.HexStringToBytes(evaluated.Replace(" ", ""));
                }
                else
                {
                    fieldValues[fieldName] = evaluated;
                }
            }

            return parser.BuildFrame(fieldValues);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 插值表达式处理 - 将 {fieldName} 替换为解析帧中对应字段的值
    /// </summary>
    private static string InterpolateExpression(string expression, ParsedFrame? parsedFrame)
    {
        if (parsedFrame == null || !expression.Contains('{'))
            return expression;

        return Regex.Replace(expression, @"\{(\w+)\}", match =>
        {
            var fieldName = match.Groups[1].Value;
            var field = parsedFrame.GetField(fieldName);
            return field?.Value?.ToString() ?? match.Value;
        });
    }
}
