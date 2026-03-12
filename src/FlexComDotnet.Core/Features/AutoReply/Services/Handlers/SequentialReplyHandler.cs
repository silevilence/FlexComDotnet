using System.Text;
using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.AutoReply.Services.Handlers;

/// <summary>
/// 顺序回复处理器 - 按预设顺序依次回复
/// </summary>
public class SequentialReplyHandler : IReplyHandler
{
    private readonly IProtocolParserService? _protocolParserService;

    public SequentialReplyHandler(IProtocolParserService? protocolParserService = null)
    {
        _protocolParserService = protocolParserService;
    }

    /// <inheritdoc/>
    public ReplyMode Mode => ReplyMode.Sequential;

    /// <inheritdoc/>
    public string DisplayName => "顺序回复";

    /// <inheritdoc/>
    public string Description => "每次收到数据后，按预设顺序发送列表中的下一帧，支持循环";

    /// <inheritdoc/>
    public ReplyResult Process(byte[] receivedData, AutoReplyRule rule)
    {
        if (receivedData.Length == 0 || rule.SequentialConfig == null)
        {
            return ReplyResult.NoReply;
        }

        var seqConfig = rule.SequentialConfig;
        var enabledFrames = seqConfig.Frames
            .Where(f => f.IsEnabled)
            .OrderBy(f => f.SortOrder)
            .ToList();

        if (enabledFrames.Count == 0)
        {
            return ReplyResult.NoReply;
        }

        // 查找当前索引对应的有效帧
        var currentIndex = seqConfig.CurrentIndex;

        // 如果索引超出范围，处理循环逻辑
        if (currentIndex >= enabledFrames.Count)
        {
            if (seqConfig.EnableLoop)
            {
                currentIndex = 0;
                seqConfig.CurrentIndex = 0;
            }
            else
            {
                return ReplyResult.NoReply;
            }
        }

        // 尝试获取有效的帧数据（跳过空内容）
        var startIndex = currentIndex;
        var checkedCount = 0;

        while (checkedCount < enabledFrames.Count)
        {
            var frame = enabledFrames[currentIndex];
            var responseData = GetFrameData(frame);

            if (responseData.Length > 0)
            {
                // 更新索引到下一个位置
                seqConfig.CurrentIndex = currentIndex + 1;
                return ReplyResult.Reply(responseData, $"{rule.Name}: {frame.Name}");
            }

            // 帧内容为空，跳到下一个
            currentIndex++;
            checkedCount++;

            if (currentIndex >= enabledFrames.Count)
            {
                if (seqConfig.EnableLoop)
                {
                    currentIndex = 0;
                }
                else
                {
                    break;
                }
            }

            // 防止无限循环（如果回到了起点）
            if (currentIndex == startIndex)
            {
                break;
            }
        }

        return ReplyResult.NoReply;
    }

    /// <inheritdoc/>
    public void Reset(AutoReplyRule rule)
    {
        if (rule.SequentialConfig != null)
        {
            rule.SequentialConfig.CurrentIndex = 0;
        }
    }

    /// <summary>
    /// 获取帧数据 - 支持纯文本和协议组帧两种模式
    /// </summary>
    private byte[] GetFrameData(SequentialFrame frame)
    {
        if (frame.ResponseMode == ResponseBuildMode.ProtocolBuild)
        {
            if (frame.ProtocolResponse == null)
                return [];
            return BuildProtocolFrame(frame.ProtocolResponse);
        }

        if (string.IsNullOrEmpty(frame.Content))
        {
            return [];
        }

        if (frame.IsHexMode)
        {
            return HexHelper.HexStringToBytes(frame.Content);
        }
        else
        {
            return Encoding.ASCII.GetBytes(frame.Content);
        }
    }

    /// <summary>
    /// 使用协议动态构建帧
    /// </summary>
    private byte[] BuildProtocolFrame(ProtocolResponseConfig responseConfig)
    {
        if (_protocolParserService == null || string.IsNullOrEmpty(responseConfig.ProtocolName))
            return [];

        var parser = _protocolParserService.GetParser(responseConfig.ProtocolName);
        if (parser == null)
            return [];

        try
        {
            var fieldValues = new Dictionary<string, object>();
            foreach (var (fieldName, expression) in responseConfig.FieldValues)
            {
                if (!string.IsNullOrEmpty(expression))
                {
                    // Hex 模式：将 hex 字符串转换为 byte[]
                    if (responseConfig.FieldHexModes.TryGetValue(fieldName, out var isHex) && isHex)
                    {
                        fieldValues[fieldName] = Serial.Helpers.HexHelper.HexStringToBytes(expression.Replace(" ", ""));
                    }
                    else
                    {
                        fieldValues[fieldName] = expression;
                    }
                }
            }

            return parser.BuildFrame(fieldValues);
        }
        catch
        {
            return [];
        }
    }
}
