using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.Scripting.Services;

namespace FlexComDotnet.Core.Features.AutoReply.Services.Handlers;

/// <summary>
/// 脚本回复处理器 - 使用 Lua 脚本处理复杂应答逻辑
/// </summary>
public class ScriptReplyHandler : IReplyHandler
{
    private readonly IScriptHookService _hookService;

    /// <inheritdoc/>
    public ReplyMode Mode => ReplyMode.Script;

    /// <inheritdoc/>
    public string DisplayName => "脚本回复";

    /// <inheritdoc/>
    public string Description => "使用 Lua 脚本实现复杂的条件判断应答逻辑";

    public ScriptReplyHandler(IScriptHookService hookService)
    {
        _hookService = hookService;
    }

    /// <inheritdoc/>
    public ReplyResult Process(byte[] receivedData, AutoReplyConfig config)
    {
        if (receivedData.Length == 0)
        {
            return ReplyResult.NoReply;
        }

        if (string.IsNullOrEmpty(config.ScriptConfig.ScriptId))
        {
            return ReplyResult.NoReply;
        }

        var result = _hookService.ExecuteReplyHookAsync(receivedData).GetAwaiter().GetResult();

        if (!result.Success)
        {
            return ReplyResult.NoReply;
        }

        if (result.ShouldReply && result.ReplyData != null && result.ReplyData.Length > 0)
        {
            return ReplyResult.Reply(result.ReplyData, "脚本回复");
        }

        return ReplyResult.NoReply;
    }

    /// <inheritdoc/>
    public void Reset(AutoReplyConfig config)
    {
    }
}
