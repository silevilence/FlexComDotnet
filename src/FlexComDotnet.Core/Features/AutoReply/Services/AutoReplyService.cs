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

        // 订阅串口数据接收事件
        _serialPortService.DataReceived += OnDataReceived;
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
    /// 处理接收到的数据
    /// </summary>
    private void OnDataReceived(object? sender, byte[] data)
    {
        if (!IsRunning || !Config.IsEnabled || data.Length == 0)
        {
            return;
        }

        // 异步处理以避免阻塞串口接收
        Task.Run(() => ProcessDataAsync(data));
    }

    /// <summary>
    /// 异步处理数据 - 遍历所有启用的规则按优先级执行
    /// </summary>
    private async Task ProcessDataAsync(byte[] data)
    {
        lock (_lockObj)
        {
            ReceiveCount++;
        }

        // 应用全局延迟
        if (Config.GlobalDelayMs > 0)
        {
            await Task.Delay(Config.GlobalDelayMs);
        }

        // 获取所有启用的规则，按优先级排序
        List<AutoReplyRule> enabledRules;
        lock (_lockObj)
        {
            enabledRules = Config.Rules
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.SortOrder)
                .ToList();
        }

        // 遍历所有启用的规则，依次处理
        foreach (var rule in enabledRules)
        {
            if (!_handlers.TryGetValue(rule.Type, out var handler))
            {
                continue;
            }

            ReplyResult result;
            lock (_lockObj)
            {
                result = handler.Process(data, rule);
            }

            if (result.ShouldReply && result.ResponseData.Length > 0)
            {
                var sent = _serialPortService.Send(result.ResponseData);

                if (sent)
                {
                    lock (_lockObj)
                    {
                        ReplyCount++;
                    }

                    ReplyTriggered?.Invoke(this, new ReplyEventArgs
                    {
                        ReceivedData = data,
                        ReplyData = result.ResponseData,
                        RuleName = result.MatchedRuleName,
                        Timestamp = DateTime.Now
                    });
                }
            }
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
            _serialPortService.DataReceived -= OnDataReceived;
        }

        _disposed = true;
    }
}
