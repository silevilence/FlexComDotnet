using System.Text.RegularExpressions;
using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;

namespace FlexComDotnet.Core.Features.AutoReply.Services.Handlers;

/// <summary>
/// 协议回复处理器 - 根据协议定义动态构建回复帧
/// </summary>
public class ProtocolReplyHandler : IReplyHandler
{
    private readonly IProtocolParserService _parserService;

    public ReplyMode Mode => ReplyMode.Protocol;
    public string DisplayName => "协议回复";
    public string Description => "根据协议定义和配置参数动态构建回复帧";

    public ProtocolReplyHandler(IProtocolParserService parserService)
    {
        _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
    }

    public ReplyResult Process(byte[] receivedData, AutoReplyRule rule)
    {
        if (rule.ProtocolConfig == null)
            return ReplyResult.NoReply;

        var protocolConfig = rule.ProtocolConfig;

        if (string.IsNullOrEmpty(protocolConfig.ProtocolName))
            return ReplyResult.NoReply;

        var parser = _parserService.GetParser(protocolConfig.ProtocolName);
        if (parser == null)
            return ReplyResult.NoReply;

        try
        {
            // 尝试解析接收到的数据以提取上下文变量
            ParsedFrame? parsedReceived = null;
            try
            {
                if (parser.Validate(receivedData))
                {
                    parsedReceived = parser.Parse(receivedData);
                }
            }
            catch
            {
                // 忽略解析错误
            }

            var fieldValues = EvaluateFieldValues(protocolConfig.FieldValues, parsedReceived);
            var frameData = parser.BuildFrame(fieldValues);
            return ReplyResult.Reply(frameData, $"协议回复: {rule.Name}");
        }
        catch
        {
            return ReplyResult.NoReply;
        }
    }

    public void Reset(AutoReplyRule rule)
    {
    }

    /// <summary>
    /// 评估字段值表达式 - 支持 {} 插值引用接收帧中的字段值
    /// </summary>
    private static Dictionary<string, object> EvaluateFieldValues(
        Dictionary<string, string> fieldExpressions,
        ParsedFrame? parsedReceived)
    {
        var result = new Dictionary<string, object>();

        foreach (var (fieldName, expression) in fieldExpressions)
        {
            if (string.IsNullOrEmpty(expression))
                continue;

            var evaluated = InterpolateExpression(expression, parsedReceived);
            result[fieldName] = evaluated;
        }

        return result;
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
