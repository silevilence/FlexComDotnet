using FlexComDotnet.Core.Features.AutoReply.Models;
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
            var fieldValues = EvaluateFieldValues(protocolConfig.FieldValues, receivedData, parser);
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
    /// 评估字段值表达式
    /// </summary>
    private static Dictionary<string, object> EvaluateFieldValues(
        Dictionary<string, string> fieldExpressions,
        byte[] receivedData,
        IProtocolParser parser)
    {
        var result = new Dictionary<string, object>();

        // Try to parse received data to extract context
        Protocol.Models.ParsedFrame? parsedReceived = null;
        try
        {
            if (parser.Validate(receivedData))
            {
                parsedReceived = parser.Parse(receivedData);
            }
        }
        catch
        {
            // Ignore parse errors for received data
        }

        foreach (var (fieldName, expression) in fieldExpressions)
        {
            if (string.IsNullOrEmpty(expression))
                continue;

            var evaluated = EvaluateExpression(expression, receivedData, parsedReceived);
            result[fieldName] = evaluated;
        }

        return result;
    }

    /// <summary>
    /// 评估单个表达式
    /// 支持简单的插值语法: 纯值、十六进制字面量
    /// </summary>
    private static object EvaluateExpression(string expression, byte[] receivedData, Protocol.Models.ParsedFrame? parsedReceived)
    {
        // Simple value - return as-is (string form, will be converted by BuildFrame)
        return expression;
    }
}
