using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services.Handlers;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.AutoReply.Services;

/// <summary>
/// 自动回复服务实现 - 支持多规则并发触发
/// </summary>
public class AutoReplyService : IAutoReplyService, IDisposable
{
    private readonly ISerialPortService _serialPortService;
    private readonly Dictionary<ReplyMode, IReplyHandler> _handlers;
    private readonly List<IReplyHandler> _handlerList;
    private readonly object _lockObj = new();
    private readonly List<byte[]> _frameWindow = [];
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    /// <inheritdoc/>
    public AutoReplyConfig Config { get; private set; } = new();

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public int ReceiveCount { get; private set; }

    /// <inheritdoc/>
    public int ReplyCount { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<ReplyEventArgs>? ReplyTriggered;

    public AutoReplyService(ISerialPortService serialPortService, IScriptHookService? scriptHookService = null, IProtocolParserService? protocolParserService = null)
    {
        _serialPortService = serialPortService;

        // 注册所有处理器
        _handlerList =
        [
            new MatchReplyHandler(protocolParserService),
            new SequentialReplyHandler(protocolParserService)
        ];

        // 如果有脚本 Hook 服务，注册脚本回复处理器
        if (scriptHookService != null)
        {
            _handlerList.Add(new ScriptReplyHandler(scriptHookService));
        }

        // 如果有协议解析服务，注册协议回复处理器
        if (protocolParserService != null)
        {
            _handlerList.Add(new ProtocolReplyHandler(protocolParserService));
        }

        _handlers = _handlerList.ToDictionary(h => h.Mode);

        // 订阅串口帧接收事件
        _serialPortService.FrameReceived += OnFrameReceived;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IReplyHandler> GetAllHandlers() => _handlerList;

    /// <inheritdoc/>
    public IReplyHandler GetHandler(ReplyMode mode)
    {
        if (_handlers.TryGetValue(mode, out var handler))
        {
            return handler;
        }

        throw new ArgumentException($"不支持的回复模式: {mode}", nameof(mode));
    }

    /// <inheritdoc/>
    public void Start()
    {
        IsRunning = true;
    }

    /// <inheritdoc/>
    public void Stop()
    {
        IsRunning = false;
    }

    /// <inheritdoc/>
    public void UpdateConfig(AutoReplyConfig config)
    {
        lock (_lockObj)
        {
            Config = config;
        }
    }

    /// <inheritdoc/>
    public void ResetCounters()
    {
        lock (_lockObj)
        {
            ReceiveCount = 0;
            ReplyCount = 0;
        }
    }

    /// <inheritdoc/>
    public void ResetHandlerState()
    {
        lock (_lockObj)
        {
            foreach (var rule in Config.Rules)
            {
                if (_handlers.TryGetValue(rule.Type, out var handler))
                {
                    handler.Reset(rule);
                }
            }
        }
    }

    /// <inheritdoc/>
    public void ResetRuleState(string ruleId)
    {
        lock (_lockObj)
        {
            var rule = Config.Rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null && _handlers.TryGetValue(rule.Type, out var handler))
            {
                handler.Reset(rule);
            }
        }
    }

    /// <summary>
    /// 处理接收到的帧 — 加入防抖窗口，等待窗口关闭后统一决策
    /// </summary>
    private void OnFrameReceived(object? sender, byte[] frame)
    {
        if (!IsRunning || !Config.IsEnabled || frame.Length == 0)
            return;

        CancellationTokenSource newCts;
        lock (_lockObj)
        {
            ReceiveCount++;
            _frameWindow.Add(frame);

            // 限制窗口大小，防止内存无限增长
            if (_frameWindow.Count > 1000)
            {
                _frameWindow.RemoveAt(0);
            }

            // 在锁内重置防抖计时器，避免竞态条件
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            newCts = _debounceCts = new CancellationTokenSource();
        }

        _ = DebounceWaitAsync(Config.DebounceWindowMs, newCts.Token);
    }

    /// <summary>
    /// 防抖等待 — 若在窗口期内被新帧重置则放弃处理
    /// </summary>
    private async Task DebounceWaitAsync(int debounceMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(debounceMs, ct);
        }
        catch (TaskCanceledException)
        {
            return; // 被新帧重置，放弃处理
        }

        ProcessWindow();
    }

    /// <summary>
    /// 处理防抖窗口内的所有帧 — 应用决策模式后分派给处理器
    /// </summary>
    private void ProcessWindow()
    {
        byte[][] snapshot;
        List<AutoReplyRule> enabledRules;

        lock (_lockObj)
        {
            snapshot = _frameWindow.ToArray();
            _frameWindow.Clear();

            enabledRules = Config.Rules
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.SortOrder)
                .ToList();
        }

        if (snapshot.Length == 0) return;

        // 分离顺序回复规则和匹配类规则
        var matchingRules = enabledRules
            .Where(r => r.Type != ReplyMode.Sequential)
            .ToList();
        var sequentialRules = enabledRules
            .Where(r => r.Type == ReplyMode.Sequential)
            .ToList();

        // 决策引擎判定
        var decision = EvaluateDecision(snapshot, matchingRules, Config.DecisionMode);

        // 执行匹配的规则
        if (decision.ShouldReply && decision.RelevantFrame != null)
        {
            foreach (var rule in decision.MatchedRules)
            {
                if (_handlers.TryGetValue(rule.Type, out var handler))
                {
                    ReplyResult result;
                    lock (_lockObj)
                    {
                        result = handler.Process(decision.RelevantFrame, rule);
                    }

                    ExecuteReply(result, decision.RelevantFrame);
                }
            }
        }

        // 顺序回复规则独立执行（不受决策模式约束）
        var lastFrame = snapshot.Last();
        foreach (var rule in sequentialRules)
        {
            if (_handlers.TryGetValue(rule.Type, out var handler))
            {
                ReplyResult result;
                lock (_lockObj)
                {
                    result = handler.Process(lastFrame, rule);
                }

                ExecuteReply(result, lastFrame);
            }
        }
    }

    /// <summary>
    /// 多帧决策引擎 — 根据决策模式判定是否触发回复
    /// </summary>
    private (bool ShouldReply, List<AutoReplyRule> MatchedRules, byte[]? RelevantFrame) EvaluateDecision(
        byte[][] frames, List<AutoReplyRule> matchingRules, DecisionMode mode)
    {
        if (matchingRules.Count == 0 || frames.Length == 0)
            return (false, [], null);

        var matchedRules = new List<AutoReplyRule>();
        byte[]? relevantFrame = null;

        switch (mode)
        {
            case DecisionMode.AND:
                foreach (var frame in frames)
                {
                    var frameMatched = false;
                    foreach (var rule in matchingRules)
                    {
                        if (!_handlers.TryGetValue(rule.Type, out var handler))
                            continue;

                        var result = handler.Process(frame, rule);
                        if (result.ShouldReply)
                        {
                            frameMatched = true;
                            if (!matchedRules.Contains(rule))
                                matchedRules.Add(rule);
                            break;
                        }
                    }
                    if (!frameMatched)
                        return (false, [], null); // 任一帧不匹配 → 放弃整个窗口
                }
                relevantFrame = frames.Last();
                break;

            case DecisionMode.OR:
                foreach (var frame in frames)
                {
                    foreach (var rule in matchingRules)
                    {
                        if (!_handlers.TryGetValue(rule.Type, out var handler))
                            continue;

                        var result = handler.Process(frame, rule);
                        if (result.ShouldReply)
                        {
                            relevantFrame = frame;
                            matchedRules.Add(rule);
                            goto Done;
                        }
                    }
                }
                Done:
                break;

            case DecisionMode.LAST:
                relevantFrame = frames.Last();
                foreach (var rule in matchingRules)
                {
                    if (!_handlers.TryGetValue(rule.Type, out var handler))
                        continue;

                    var result = handler.Process(relevantFrame, rule);
                    if (result.ShouldReply)
                        matchedRules.Add(rule);
                }
                break;

            case DecisionMode.FIRST:
                relevantFrame = frames.First();
                foreach (var rule in matchingRules)
                {
                    if (!_handlers.TryGetValue(rule.Type, out var handler))
                        continue;

                    var result = handler.Process(relevantFrame, rule);
                    if (result.ShouldReply)
                        matchedRules.Add(rule);
                }
                break;
        }

        if (matchedRules.Count > 0 && relevantFrame != null)
            return (true, matchedRules, relevantFrame);

        return (false, [], null);
    }

    /// <summary>
    /// 执行回复 — 发送数据并触发事件通知
    /// </summary>
    private void ExecuteReply(ReplyResult result, byte[]? receivedData = null)
    {
        if (!result.ShouldReply || result.ResponseData.Length == 0)
            return;

        var sent = _serialPortService.Send(result.ResponseData);
        if (sent)
        {
            lock (_lockObj)
            {
                ReplyCount++;
            }

            ReplyTriggered?.Invoke(this, new ReplyEventArgs
            {
                ReceivedData = receivedData ?? result.ResponseData,
                ReplyData = result.ResponseData,
                RuleName = result.MatchedRuleName,
                Timestamp = DateTime.Now
            });
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Stop();
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _serialPortService.FrameReceived -= OnFrameReceived;
        }

        _disposed = true;
    }
}
