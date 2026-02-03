using FlexComDotnet.Core.Features.AutoReply.Models;

namespace FlexComDotnet.Core.Features.AutoReply.Services;

/// <summary>
/// 自动回复服务接口
/// </summary>
public interface IAutoReplyService
{
    /// <summary>
    /// 当前配置
    /// </summary>
    AutoReplyConfig Config { get; }

    /// <summary>
    /// 是否已启动监听
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 接收计数
    /// </summary>
    int ReceiveCount { get; }

    /// <summary>
    /// 回复计数
    /// </summary>
    int ReplyCount { get; }

    /// <summary>
    /// 回复事件（用于日志记录）
    /// </summary>
    event EventHandler<ReplyEventArgs>? ReplyTriggered;

    /// <summary>
    /// 启动自动回复监听
    /// </summary>
    void Start();

    /// <summary>
    /// 停止自动回复监听
    /// </summary>
    void Stop();

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="config">新配置</param>
    void UpdateConfig(AutoReplyConfig config);

    /// <summary>
    /// 重置统计计数
    /// </summary>
    void ResetCounters();

    /// <summary>
    /// 重置当前处理器状态
    /// </summary>
    void ResetHandlerState();

    /// <summary>
    /// 获取所有可用的回复处理器
    /// </summary>
    IReadOnlyList<IReplyHandler> GetAllHandlers();

    /// <summary>
    /// 获取指定模式的处理器
    /// </summary>
    IReplyHandler GetHandler(ReplyMode mode);
}

/// <summary>
/// 回复事件参数
/// </summary>
public class ReplyEventArgs : EventArgs
{
    /// <summary>
    /// 触发回复的接收数据
    /// </summary>
    public byte[] ReceivedData { get; init; } = [];

    /// <summary>
    /// 回复数据
    /// </summary>
    public byte[] ReplyData { get; init; } = [];

    /// <summary>
    /// 匹配的规则名称
    /// </summary>
    public string? RuleName { get; init; }

    /// <summary>
    /// 回复时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
