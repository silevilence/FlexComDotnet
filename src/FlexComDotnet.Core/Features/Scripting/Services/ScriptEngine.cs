using System.Diagnostics;
using System.Text;
using FlexComDotnet.Core.Features.Scripting.Models;
using NLua;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// Lua 脚本引擎实现 - 基于 NLua
/// </summary>
public class ScriptEngine : IScriptEngine
{
    private Lua? _lua;
    private IScriptApiBridge? _bridge;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private bool _disposed;

    /// <inheritdoc />
    public ScriptState State { get; private set; } = ScriptState.Idle;

    /// <inheritdoc />
    public string? CurrentScriptName { get; private set; }

    /// <inheritdoc />
    public event EventHandler<ScriptState>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<ScriptLogEntry>? LogOutput;

    /// <inheritdoc />
    public event EventHandler<string>? ErrorOccurred;

    /// <inheritdoc />
    public void RegisterApiBridge(IScriptApiBridge bridge)
    {
        _bridge = bridge;
        _bridge.LogOutput += OnBridgeLogOutput;
    }

    /// <inheritdoc />
    public async Task<ScriptExecutionResult> ExecuteAsync(string scriptCode, string scriptName, CancellationToken cancellationToken = default)
    {
        if (!_executionLock.Wait(0))
        {
            return ScriptExecutionResult.Failed("另一个脚本正在运行，请先停止当前脚本");
        }

        try
        {
            return await ExecuteInternalAsync(scriptCode, scriptName, cancellationToken);
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private async Task<ScriptExecutionResult> ExecuteInternalAsync(string scriptCode, string scriptName, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sw = Stopwatch.StartNew();

        CurrentScriptName = scriptName;
        SetState(ScriptState.Running);

        // 配置桥接对象
        _bridge?.SetScriptName(scriptName);
        _bridge?.SetCancellationToken(_cts.Token);

        try
        {
            // 在新线程中执行 Lua 脚本，避免阻塞调用线程
            await Task.Run(() =>
            {
                using var lua = new Lua();
                _lua = lua;

                // 设置 UTF-8 编码，避免中文等非 ASCII 字符乱码
                lua.State.Encoding = Encoding.UTF8;

                // 注册 FCom API 桥接
                RegisterLuaApi(lua);

                // 注册中断检查 hook
                RegisterDebugHook(lua, _cts.Token);

                lua.DoString(scriptCode, scriptName);

                _lua = null;
            }, _cts.Token);

            sw.Stop();
            SetState(ScriptState.Idle);
            return ScriptExecutionResult.Succeeded(sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _lua = null;
            SetState(ScriptState.Idle);
            return ScriptExecutionResult.Failed("脚本已被用户停止", sw.ElapsedMilliseconds);
        }
        catch (NLua.Exceptions.LuaException ex)
        {
            sw.Stop();
            _lua = null;
            var errorMsg = ex.Message;

            // debug hook 中抛出的取消异常会被 NLua 包装为 LuaException
            if (_cts?.IsCancellationRequested == true || errorMsg.Contains("脚本已被用户停止"))
            {
                SetState(ScriptState.Idle);
                return ScriptExecutionResult.Failed("脚本已被用户停止", sw.ElapsedMilliseconds);
            }

            SetState(ScriptState.Error);
            ErrorOccurred?.Invoke(this, errorMsg);
            SetState(ScriptState.Idle);
            return ScriptExecutionResult.Failed(errorMsg, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _lua = null;
            var errorMsg = ex.Message;

            if (_cts?.IsCancellationRequested == true)
            {
                SetState(ScriptState.Idle);
                return ScriptExecutionResult.Failed("脚本已被用户停止", sw.ElapsedMilliseconds);
            }

            SetState(ScriptState.Error);
            ErrorOccurred?.Invoke(this, errorMsg);
            SetState(ScriptState.Idle);
            return ScriptExecutionResult.Failed(errorMsg, sw.ElapsedMilliseconds);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void RegisterLuaApi(Lua lua)
    {
        if (_bridge == null) return;

        // 创建 FCom 全局表
        lua.NewTable("FCom");

        // 注册各个 API 函数
        lua.RegisterFunction("FCom.send", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Send))!);
        lua.RegisterFunction("FCom.sendBytes", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.SendBytes))!);
        lua.RegisterFunction("FCom.sendText", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.SendText))!);
        lua.RegisterFunction("FCom.log", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Log))!);
        lua.RegisterFunction("FCom.logDebug", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.LogDebug))!);
        lua.RegisterFunction("FCom.logWarning", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.LogWarning))!);
        lua.RegisterFunction("FCom.logError", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.LogError))!);
        lua.RegisterFunction("FCom.delay", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Delay))!);
        lua.RegisterFunction("FCom.crc16", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Crc16))!);
        lua.RegisterFunction("FCom.crc32", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Crc32))!);
        lua.RegisterFunction("FCom.checksum", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.Checksum))!);
        lua.RegisterFunction("FCom.getTimestamp", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.GetTimestamp))!);
        lua.RegisterFunction("FCom.hexToBytes", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.HexToBytes))!);
        lua.RegisterFunction("FCom.bytesToHex", _bridge, typeof(IScriptApiBridge).GetMethod(nameof(IScriptApiBridge.BytesToHex))!);
    }

    private static void RegisterDebugHook(Lua lua, CancellationToken token)
    {
        // 使用 Lua 层 debug.sethook 调用注册的 C# 函数实现中断检查
        // 直接从 C# DebugHook 事件抛异常无法正确传播回 Lua VM，
        // 但通过 lua.RegisterFunction 注册的函数抛异常可以被 NLua 正确处理
        var checker = new CancelChecker(token);
        lua.RegisterFunction("__checkCancel", checker,
            typeof(CancelChecker).GetMethod(nameof(CancelChecker.Check))!);

        // 每 100 条 VM 指令调用一次取消检查（确保单行紧凑循环也能被中断）
        lua.DoString("debug.sethook(function() __checkCancel() end, '', 100)");
    }

    /// <summary>
    /// 取消检查器 - 供 Lua debug hook 回调
    /// </summary>
    private sealed class CancelChecker(CancellationToken token)
    {
        public void Check()
        {
            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException("脚本已被用户停止");
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        _cts?.Cancel();
    }

    private void SetState(ScriptState newState)
    {
        if (State != newState)
        {
            State = newState;
            StateChanged?.Invoke(this, newState);
        }
    }

    private void OnBridgeLogOutput(object? sender, ScriptLogEntry entry)
    {
        LogOutput?.Invoke(this, entry);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _lua = null;

        if (_bridge != null)
        {
            _bridge.LogOutput -= OnBridgeLogOutput;
        }

        _executionLock.Dispose();
        State = ScriptState.Idle;

        GC.SuppressFinalize(this);
    }
}
