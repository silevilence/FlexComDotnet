using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

/// <summary>Versioned bounded shared queue. The producer never waits for this consumer.</summary>
public sealed class NativeProcessMonitorTransport(string? nativeRoot = null) : IProcessMonitorTransport
{
    private const int HeaderSize = 64, RecordSize = 2464, Capacity = 256;
    private readonly string _nativeRoot = nativeRoot ?? Path.Combine(AppContext.BaseDirectory, "ProcessHook");
    private MemoryMappedFile? _mapping;
    private MemoryMappedViewAccessor? _view;
    private Mutex? _queueMutex;
    private Semaphore? _owner;
    private bool _ownsSession;
    private CancellationTokenSource? _stop;
    private Task? _reader;
    private Process? _targetProcess;
    private ProcessMonitorRecordStream? _records;
    public event EventHandler<ProcessMonitorRecord>? RecordReceived;
    public event EventHandler<string>? DisconnectedUnexpectedly;

    public async Task ConnectAsync(ProcessMonitorTarget target, Guid sessionId, CancellationToken cancellationToken, ProcessMonitorLaunch? launch = null)
    {
        if (_mapping is not null) throw new InvalidOperationException("采集连接已存在。");
        if (target.ProcessId == Environment.ProcessId) throw new InvalidOperationException("不能监控采集应用自身。");
        if (target.Architecture is not ("x86" or "x64")) throw new NotSupportedException("首期仅支持 x86/x64 目标。");
        var helper = Path.Combine(_nativeRoot, target.Architecture, "FlexCom.ProcessHook.Helper.exe");
        var module = Path.Combine(_nativeRoot, target.Architecture, "FlexCom.ProcessHook.dll");
        if (!File.Exists(helper) || !File.Exists(module)) throw new FileNotFoundException($"缺少 {target.Architecture} 采集组件，请构建或安装完整包。");
        var name = $"Local\\FlexCom.ProcessHook.v1.{target.ProcessId}.{target.StartTime}";
        try
        {
            _targetProcess = Process.GetProcessById(target.ProcessId);
            if (_targetProcess.StartTime.ToUniversalTime().ToFileTimeUtc() != target.StartTime) throw new InvalidOperationException("目标已重启，请重新选择。");
            _records = new(target, sessionId);
            _owner = new Semaphore(1, 1, name + ".owner");
            if (!(_ownsSession = _owner.WaitOne(0))) throw new InvalidOperationException("该进程已由另一采集会话监控。");
            _queueMutex = new Mutex(false, name + ".lock");
            _mapping = MemoryMappedFile.CreateOrOpen(name, HeaderSize + RecordSize * Capacity);
            _view = _mapping.CreateViewAccessor();
            WithQueue(() =>
            {
                var version = _view.ReadUInt32(0);
                if (version != 0 && (version != 2 || _view.ReadUInt32(4) != RecordSize)) throw new IOException("采集组件版本不匹配；请重启目标后重试。");
                _view.Write(12, 0u);
                _view.Write(0, 2u); _view.Write(4, RecordSize); _view.Write(8, Capacity);
                _view.WriteArray(16, sessionId.ToByteArray(), 0, 16);
                _view.Write(32, 0u); _view.Write(36, 0u); _view.Write(40, 0u);
                _view.Write(48, 0u); _view.Write(56, 0ul);
                _view.Write(52, launch is null ? 0u : 1u);
                _view.Write(12, 1u);
            });
            if (launch is not null) await launch.ActivateAsync(cancellationToken).ConfigureAwait(false);
            else
            {
                var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                foreach (var argument in new[] { "--attach", target.ProcessId.ToString(), target.StartTime.ToString(), module }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start) ?? throw new IOException("无法启动采集助手。");
                var error = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
                if (process.ExitCode != 0) throw new IOException(await error.ConfigureAwait(false));
            }
            for (var i = 0; i < 250 && _view.ReadUInt32(44) == 0 && !_targetProcess.HasExited; i++) await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            if (_view.ReadUInt32(44) != 1) throw new IOException("采集模块尚未就绪。");
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoopAsync(_stop.Token), CancellationToken.None);
        }
        catch { await DisconnectAsync().ConfigureAwait(false); throw; }
    }

    private void WithQueue(Action action)
    {
        var acquired = false;
        try
        {
            try { acquired = _queueMutex!.WaitOne(1000); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("采集队列锁超时。");
            action();
        }
        finally { if (acquired) _queueMutex!.ReleaseMutex(); }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        string? reason = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var records = new List<byte[]>();
                uint dropped = 0;
                WithQueue(() =>
                {
                    var count = _view!.ReadUInt32(40); var index = _view.ReadUInt32(32);
                    if (count > Capacity || index >= Capacity) throw new IOException("采集队列损坏。");
                    dropped = _view.ReadUInt32(48);
                    for (var i = 0; i < count; i++)
                    {
                        var bytes = new byte[RecordSize];
                        _view.ReadArray(HeaderSize + index * RecordSize, bytes, 0, bytes.Length);
                        records.Add(bytes); index = (index + 1) % Capacity;
                    }
                    dropped = _view.ReadUInt32(48);
                    _view.Write(32, index); _view.Write(40, 0u);
                });
                foreach (var record in _records!.ReadBatch(records, dropped)) RecordReceived?.Invoke(this, record);
                if (_targetProcess!.HasExited) { reason = "目标进程已退出"; return; }
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { reason = "采集连接中断：" + error.Message; }
        finally
        {
            // Release ownership before reporting disconnection, so another session can recover immediately.
            try { CloseConnection(); }
            catch (Exception error) { reason = (reason ?? "采集连接已停止") + "；清理失败：" + error.Message; }
            if (reason is not null) DisconnectedUnexpectedly?.Invoke(this, reason);
        }
    }

    public async Task DisconnectAsync()
    {
        _stop?.Cancel();
        if (_reader is not null) await _reader.ConfigureAwait(false);
        _reader = null; _stop?.Dispose(); _stop = null;
        CloseConnection();
    }

    private void CloseConnection()
    {
        try
        {
            if (_view is not null && _ownsSession)
            {
                try { WithQueue(() => _view.Write(12, 0u)); }
                catch (IOException) { _view.Write(12, 0u); } // Atomic aligned stop flag; never retain a broken queue's ownership.
            }
        }
        finally
        {
            _view?.Dispose(); _view = null; _mapping?.Dispose(); _mapping = null;
            _queueMutex?.Dispose(); _queueMutex = null;
            if (_ownsSession) _owner!.Release();
            _ownsSession = false; _owner?.Dispose(); _owner = null;
            _targetProcess?.Dispose(); _targetProcess = null;
        }
    }
    public ValueTask DisposeAsync() => new(DisconnectAsync());
}
