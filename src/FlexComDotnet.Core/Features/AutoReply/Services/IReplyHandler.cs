using FlexComDotnet.Core.Features.AutoReply.Models;

namespace FlexComDotnet.Core.Features.AutoReply.Services;

/// <summary>
/// 回复处理器接口（策略模式）
/// </summary>
public interface IReplyHandler
{
    /// <summary>
    /// 处理器支持的回复模式
    /// </summary>
    ReplyMode Mode { get; }

    /// <summary>
    /// 处理器显示名称
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 处理器描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 处理接收到的数据并返回回复结果
    /// </summary>
    /// <param name="receivedData">接收到的数据</param>
    /// <param name="config">自动回复配置</param>
    /// <returns>回复结果</returns>
    ReplyResult Process(byte[] receivedData, AutoReplyConfig config);

    /// <summary>
    /// 重置处理器状态（如顺序回复的索引）
    /// </summary>
    /// <param name="config">自动回复配置</param>
    void Reset(AutoReplyConfig config);
}
