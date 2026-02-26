using System.Diagnostics;
using System.Text;
using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Services;
using NLua;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本 Hook 服务实现
/// </summary>
public class ScriptHookService : IScriptHookService, IDisposable
{
    private readonly IScriptManager _scriptManager;
    private readonly IScriptApiBridge _apiBridge;
    private readonly IScriptEngine _scriptEngine;
    private readonly ISerialPortService _serialPortService;
    private readonly object _lockObj = new();
    private bool _disposed;

    /// <inheritdoc />
    public ScriptHookSettings Settings { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler<ScriptLogEntry>? LogOutput;

    /// <inheritdoc />
    public event EventHandler<ScriptAutoReplyEventArgs>? AutoReplySent;

    public ScriptHookService(
        IScriptManager scriptManager,
        IScriptApiBridge apiBridge,
        IScriptEngine scriptEngine,
        ISerialPortService serialPortService)
    {
        _scriptManager = scriptManager;
        _apiBridge = apiBridge;
        _scriptEngine = scriptEngine;
        _serialPortService = serialPortService;

        _serialPortService.DataReceived += OnDataReceived;
    }

    /// <inheritdoc />
    public void UpdateSettings(ScriptHookSettings settings)
    {
        lock (_lockObj)
        {
            Settings = settings;
        }
    }

    /// <inheritdoc />
    public void SetHookEnabled(HookType hookType, bool enabled)
    {
        lock (_lockObj)
        {
            var config = GetHookConfig(hookType);
            config.IsEnabled = enabled;

            // 更新串口服务的处理器
            UpdateSerialPortProcessors();
        }
    }

    /// <inheritdoc />
    public void SetHookScript(HookType hookType, string? scriptId)
    {
        lock (_lockObj)
        {
            var config = GetHookConfig(hookType);
            config.ScriptId = scriptId;

            // 更新串口服务的处理器
            UpdateSerialPortProcessors();
        }
    }

    private void UpdateSerialPortProcessors()
    {
        // Rx 预处理器
        if (Settings.RxPreProcessor.IsEnabled && !string.IsNullOrEmpty(Settings.RxPreProcessor.ScriptId))
        {
            _serialPortService.RxPreProcessor = (data) =>
            {
                var result = ExecutePipelineHookSync(HookType.RxPreProcessor, data, "onReceive");
                return result.Success && result.ProcessedData != null ? result.ProcessedData : data;
            };
        }
        else
        {
            _serialPortService.RxPreProcessor = null;
        }

        // Tx 后处理器
        if (Settings.TxPostProcessor.IsEnabled && !string.IsNullOrEmpty(Settings.TxPostProcessor.ScriptId))
        {
            _serialPortService.TxPostProcessor = (data) =>
            {
                var result = ExecutePipelineHookSync(HookType.TxPostProcessor, data, "onSend");
                return result.Success && result.ProcessedData != null ? result.ProcessedData : data;
            };
        }
        else
        {
            _serialPortService.TxPostProcessor = null;
        }
    }

    private HookExecutionResult ExecutePipelineHookSync(HookType hookType, byte[] data, string functionName)
    {
        HookConfig config;
        lock (_lockObj)
        {
            config = GetHookConfig(hookType);
        }

        if (!config.IsEnabled || string.IsNullOrEmpty(config.ScriptId))
        {
            return HookExecutionResult.SuccessWithData(data);
        }

        var scriptContent = _scriptManager.ReadScriptContent(config.ScriptId);
        if (string.IsNullOrEmpty(scriptContent))
        {
            return HookExecutionResult.Failed($"脚本不存在: {config.ScriptId}");
        }

        var sw = Stopwatch.StartNew();
        var originalHex = HexHelper.BytesToHexString(data);

        try
        {
            var processedData = ExecutePipelineScript(scriptContent, data, functionName);
            sw.Stop();

            var resultData = processedData ?? data;
            var processedHex = HexHelper.BytesToHexString(resultData);

            // 记录处理前后的数据
            var hookName = hookType == HookType.RxPreProcessor ? "Rx" : "Tx";
            if (originalHex != processedHex)
            {
                EmitLog($"[{hookName}] 原始: {originalHex}", ScriptLogLevel.Debug);
                EmitLog($"[{hookName}] 处理后: {processedHex}", ScriptLogLevel.Info);
            }
            else
            {
                EmitLog($"[{hookName}] 数据未变: {originalHex}", ScriptLogLevel.Debug);
            }

            return HookExecutionResult.SuccessWithData(resultData, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitLog($"{config.DisplayName} Hook 执行失败: {ex.Message}", ScriptLogLevel.Error);
            return HookExecutionResult.Failed(ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc />
    public async Task<HookExecutionResult> ExecuteRxPreProcessorAsync(byte[] data)
    {
        return await ExecutePipelineHookAsync(HookType.RxPreProcessor, data, "onReceive");
    }

    /// <inheritdoc />
    public async Task<HookExecutionResult> ExecuteTxPostProcessorAsync(byte[] data)
    {
        return await ExecutePipelineHookAsync(HookType.TxPostProcessor, data, "onSend");
    }

    /// <inheritdoc />
    public async Task<HookExecutionResult> ExecuteReplyHookAsync(byte[] receivedData)
    {
        HookConfig config;
        lock (_lockObj)
        {
            config = Settings.Reply;
        }

        if (!config.IsEnabled || string.IsNullOrEmpty(config.ScriptId))
        {
            return HookExecutionResult.Skipped();
        }

        var scriptContent = _scriptManager.ReadScriptContent(config.ScriptId);
        if (string.IsNullOrEmpty(scriptContent))
        {
            return HookExecutionResult.Failed($"脚本不存在: {config.ScriptId}");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await Task.Run(() => ExecuteReplyScript(scriptContent, receivedData));
            sw.Stop();

            if (result.replyData != null && result.replyData.Length > 0)
            {
                return HookExecutionResult.SuccessWithReply(result.replyData, sw.ElapsedMilliseconds);
            }

            return HookExecutionResult.SuccessNoReply(sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitLog($"Reply Hook 执行失败: {ex.Message}", ScriptLogLevel.Error);
            return HookExecutionResult.Failed(ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc />
    public async Task<ScriptExecutionResult> ExecuteTaskAsync(string scriptId, CancellationToken cancellationToken = default)
    {
        var scriptContent = _scriptManager.ReadScriptContent(scriptId);
        if (string.IsNullOrEmpty(scriptContent))
        {
            return ScriptExecutionResult.Failed($"脚本不存在: {scriptId}");
        }

        var scriptInfo = _scriptManager.GetScript(scriptId);
        var scriptName = scriptInfo?.Name ?? scriptId;

        return await _scriptEngine.ExecuteAsync(scriptContent, scriptName, cancellationToken);
    }

    private async Task<HookExecutionResult> ExecutePipelineHookAsync(HookType hookType, byte[] data, string functionName)
    {
        HookConfig config;
        lock (_lockObj)
        {
            config = GetHookConfig(hookType);
        }

        if (!config.IsEnabled || string.IsNullOrEmpty(config.ScriptId))
        {
            return HookExecutionResult.SuccessWithData(data);
        }

        var scriptContent = _scriptManager.ReadScriptContent(config.ScriptId);
        if (string.IsNullOrEmpty(scriptContent))
        {
            return HookExecutionResult.Failed($"脚本不存在: {config.ScriptId}");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var processedData = await Task.Run(() => ExecutePipelineScript(scriptContent, data, functionName));
            sw.Stop();

            return HookExecutionResult.SuccessWithData(processedData ?? data, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitLog($"{config.DisplayName} Hook 执行失败: {ex.Message}", ScriptLogLevel.Error);
            return HookExecutionResult.Failed(ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private byte[]? ExecutePipelineScript(string scriptContent, byte[] data, string functionName)
    {
        using var lua = new Lua();
        lua.State.Encoding = Encoding.UTF8;

        RegisterHookApi(lua);

        lua.DoString(scriptContent, "hook");

        var func = lua[functionName] as LuaFunction;
        if (func == null)
        {
            EmitLog($"脚本中未定义 {functionName} 函数，跳过处理", ScriptLogLevel.Warning);
            return data;
        }

        var hexInput = HexHelper.BytesToHexString(data);
        var results = func.Call(hexInput);

        if (results == null || results.Length == 0 || results[0] == null)
        {
            return data;
        }

        var resultStr = results[0].ToString();
        if (string.IsNullOrEmpty(resultStr))
        {
            return data;
        }

        return HexHelper.HexStringToBytes(resultStr);
    }

    private (byte[]? replyData, bool shouldReply) ExecuteReplyScript(string scriptContent, byte[] receivedData)
    {
        using var lua = new Lua();
        lua.State.Encoding = Encoding.UTF8;

        RegisterHookApi(lua);

        lua.DoString(scriptContent, "reply_hook");

        var func = lua["onReceive"] as LuaFunction;
        if (func == null)
        {
            EmitLog("脚本中未定义 onReceive 函数", ScriptLogLevel.Warning);
            return (null, false);
        }

        var hexInput = HexHelper.BytesToHexString(receivedData);
        var results = func.Call(hexInput);

        if (results == null || results.Length == 0 || results[0] == null)
        {
            return (null, false);
        }

        var resultStr = results[0].ToString();
        if (string.IsNullOrEmpty(resultStr))
        {
            return (null, false);
        }

        var replyData = HexHelper.HexStringToBytes(resultStr);
        return (replyData, replyData.Length > 0);
    }

    private void RegisterHookApi(Lua lua)
    {
        lua.NewTable("FCom");

        lua.RegisterFunction("FCom.log", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Log))!);
        lua.RegisterFunction("FCom.logDebug", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.LogDebug))!);
        lua.RegisterFunction("FCom.logWarning", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.LogWarning))!);
        lua.RegisterFunction("FCom.logError", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.LogError))!);
        lua.RegisterFunction("FCom.crc16", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Crc16))!);
        lua.RegisterFunction("FCom.crc32", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Crc32))!);
        lua.RegisterFunction("FCom.checksum", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Checksum))!);
        lua.RegisterFunction("FCom.getTimestamp", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.GetTimestamp))!);
        lua.RegisterFunction("FCom.hexToBytes", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.HexToBytes))!);
        lua.RegisterFunction("FCom.bytesToHex", _apiBridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.BytesToHex))!);
    }

    private HookConfig GetHookConfig(HookType hookType)
    {
        return hookType switch
        {
            HookType.RxPreProcessor => Settings.RxPreProcessor,
            HookType.TxPostProcessor => Settings.TxPostProcessor,
            HookType.Reply => Settings.Reply,
            _ => throw new ArgumentException($"不支持的 Hook 类型: {hookType}", nameof(hookType))
        };
    }

    private void EmitLog(string message, ScriptLogLevel level)
    {
        LogOutput?.Invoke(this, new ScriptLogEntry
        {
            Message = message,
            Level = level,
            ScriptName = "HookService",
            Timestamp = DateTime.Now
        });
    }

    private void OnApiBridgeLogOutput(object? sender, ScriptLogEntry entry)
    {
        LogOutput?.Invoke(this, entry);
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        if (data.Length == 0) return;

        HookConfig config;
        lock (_lockObj)
        {
            config = Settings.Reply;
        }

        if (!config.IsEnabled || string.IsNullOrEmpty(config.ScriptId))
        {
            return;
        }

        Task.Run(async () =>
        {
            var result = await ExecuteReplyHookAsync(data);
            if (result.Success && result.ShouldReply && result.ReplyData != null && result.ReplyData.Length > 0)
            {
                // 获取 Tx Hook 处理后的数据
                var processedData = result.ReplyData;
                if (_serialPortService.TxPostProcessor != null)
                {
                    processedData = _serialPortService.TxPostProcessor(result.ReplyData);
                }
                
                // 先触发事件让 ViewModel 标记，再发送（避免 HookProcessed 事件先于标记）
                AutoReplySent?.Invoke(this, new ScriptAutoReplyEventArgs(result.ReplyData, processedData));
                
                _serialPortService.Send(result.ReplyData);
                EmitLog($"脚本应答: {HexHelper.BytesToHexString(result.ReplyData)}", ScriptLogLevel.Info);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _serialPortService.DataReceived -= OnDataReceived;

        GC.SuppressFinalize(this);
    }
}
