using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

/// <summary>Versioned bounded shared queue. Producer waits are bounded; decoding occurs outside the queue lock.</summary>
public sealed class NativeProcessMonitorTransport(string? nativeRoot = null) : IProcessMonitorTransport
{
    private const int HeaderSize = 176, RecordSize = 2464, Capacity = 256, ProtocolVersion = 4;
    private const int LossStateOffset = 48, EpochOffset = 52, StartupOffset = 56, SequenceOffset = 64, DiagnosticsOffset = 72;
    private readonly string _nativeRoot = nativeRoot ?? Path.Combine(AppContext.BaseDirectory, "ProcessHook");
    private MemoryMappedFile? _mapping;
    private MemoryMappedViewAccessor? _view;
    private MemoryMappedViewStream? _recordView;
    private Mutex? _queueMutex;
    private Semaphore? _owner;
    private bool _ownsSession;
    private CancellationTokenSource? _stop;
    private Task? _reader;
    private Process? _targetProcess;
    private ProcessMonitorRecordStream? _records;
    public event EventHandler<ProcessMonitorRecord>? RecordReceived;
    public event EventHandler<string>? DisconnectedUnexpectedly;
    public ProcessMonitorDiagnostics? Diagnostics { get; private set; }

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
            _recordView = _mapping.CreateViewStream(HeaderSize, RecordSize * Capacity, MemoryMappedFileAccess.Read);
            WithQueue(() =>
            {
                var version = _view.ReadUInt32(0);
                if (version != 0 && (version != ProtocolVersion || _view.ReadUInt32(4) != RecordSize)) throw new IOException("采集组件版本不匹配；请重启目标后重试。");
                var epoch = checked(_view.ReadUInt32(EpochOffset) + 1);
                _view.Write(12, 0u);
                _view.Write(0, (uint)ProtocolVersion); _view.Write(4, RecordSize); _view.Write(8, Capacity);
                _view.WriteArray(16, sessionId.ToByteArray(), 0, 16);
                _view.Write(32, 0u); _view.Write(36, 0u); _view.Write(40, 0u);
                _view.Write(SequenceOffset, 0ul);
                _view.WriteArray(DiagnosticsOffset, new byte[ProcessMonitorDiagnostics.WireSize], 0, ProcessMonitorDiagnostics.WireSize);
                // Aligned epoch/count pairs are reset atomically. Old native CAS increments cannot hit a new session.
                var counterState = (ulong)epoch << 32;
                for (var i = 0; i < 8; ++i) ResetCounter(DiagnosticsOffset + i * sizeof(ulong), counterState);
                const int NotificationCounterOffset = 72;
                ResetCounter(DiagnosticsOffset + NotificationCounterOffset, counterState);
                ResetCounter(LossStateOffset, counterState); Diagnostics = null;
                _view.Write(StartupOffset, launch is null ? 0u : 1u);
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
            if (launch is null)
                RecordReceived?.Invoke(this, new ProcessMonitorRecord
                {
                    SessionId = sessionId, ProcessId = target.ProcessId, ProcessStartTime = target.StartTime,
                    Api = "能力提示", Direction = CaptureDirection.Control, Integrity = CaptureIntegrity.Unavailable,
                    Timestamp = DateTimeOffset.UtcNow,
                    Detail = "运行中附加无法回溯已进入的异步完成等待，可能漏采且无法确定数量；.NET SerialPort 请使用启动并监控。此提示不计入已知丢弃数。"
                });
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoopAsync(_stop.Token), CancellationToken.None);
        }
        catch { await DisconnectAsync().ConfigureAwait(false); throw; }
    }

    private unsafe void ResetCounter(int offset, ulong state)
    {
        byte* pointer = null;
        var handle = _view!.SafeMemoryMappedViewHandle;
        handle.AcquirePointer(ref pointer);
        try { Interlocked.Exchange(ref *(long*)(pointer + _view.PointerOffset + offset), unchecked((long)state)); }
        finally { handle.ReleasePointer(); }
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
            // Allocate once, before taking the interprocess mutex. Copy the ring in at most two bulk reads.
            var batch = new byte[RecordSize * Capacity];
            var diagnosticBytes = new byte[ProcessMonitorDiagnostics.WireSize];
            while (!cancellationToken.IsCancellationRequested)
            {
                uint dropped = 0, count = 0;
                WithQueue(() =>
                {
                    count = _view!.ReadUInt32(40); var index = _view.ReadUInt32(32);
                    if (count > Capacity || index >= Capacity) throw new IOException("采集队列损坏。");
                    if (count != 0)
                    {
                        var firstBytes = checked((int)Math.Min(count, Capacity - index) * RecordSize);
                        _recordView!.Position = index * RecordSize;
                        _recordView.ReadExactly(batch.AsSpan(0, firstBytes));
                        var remainingBytes = checked((int)count * RecordSize - firstBytes);
                        if (remainingBytes != 0)
                        {
                            _recordView.Position = 0;
                            _recordView.ReadExactly(batch.AsSpan(firstBytes, remainingBytes));
                        }
                    }
                    dropped = _view.ReadUInt32(LossStateOffset);
                    _view.Write(32, (index + count) % Capacity); _view.Write(40, 0u);
                    _view.ReadArray(DiagnosticsOffset, diagnosticBytes, 0, diagnosticBytes.Length);
                });
                Diagnostics = ProcessMonitorDiagnostics.FromWire(diagnosticBytes);
                var records = new List<byte[]>((int)count);
                for (var i = 0; i < count; ++i) records.Add(batch.AsSpan(i * RecordSize, RecordSize).ToArray());
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
            _recordView?.Dispose(); _recordView = null;
            _view?.Dispose(); _view = null; _mapping?.Dispose(); _mapping = null;
            _queueMutex?.Dispose(); _queueMutex = null;
            if (_ownsSession) _owner!.Release();
            _ownsSession = false; _owner?.Dispose(); _owner = null;
            _targetProcess?.Dispose(); _targetProcess = null;
        }
    }
    public ValueTask DisposeAsync() => new(DisconnectAsync());
}
