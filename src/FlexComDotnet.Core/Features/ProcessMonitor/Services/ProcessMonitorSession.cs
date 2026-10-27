using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

/// <summary>A process capture owns its transport and records; stopping never owns target termination.</summary>
public sealed class ProcessMonitorSession : IAsyncDisposable
{
    private readonly IProcessMonitorTransport _transport;
    private readonly SemaphoreSlim _lifecycle = new(1);
    private readonly object _recordLock = new();
    private readonly Queue<ProcessMonitorRecord> _records = new();
    private readonly int _historyCapacity;
    private ProcessMonitorTarget? _target;
    private bool _disposed;
    public Guid SessionId { get; private set; }
    public bool IsMonitoring { get; private set; }
    public string Status { get; private set; } = "未开始";
    public long EvictedRecords { get; private set; }
    public event EventHandler<ProcessMonitorRecord>? RecordReceived;
    public event EventHandler? StateChanged;
    public IReadOnlyList<ProcessMonitorRecord> Records { get { lock (_recordLock) return _records.ToArray(); } }
    public ProcessMonitorSnapshot Snapshot()
    {
        lock (_recordLock) return new(SessionId, _records.ToArray(), EvictedRecords);
    }

    public ProcessMonitorSession(IProcessMonitorTransport transport, int historyCapacity = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(historyCapacity, 1);
        _historyCapacity = historyCapacity;
        _transport = transport;
        _transport.RecordReceived += OnRecord;
        _transport.DisconnectedUnexpectedly += OnDisconnected;
    }

    public Task AttachAsync(ProcessMonitorTarget target, CancellationToken cancellationToken = default) => ConnectAsync(target, null, cancellationToken);
    public Task StartAsync(ProcessMonitorLaunch launch, CancellationToken cancellationToken = default) => ConnectAsync(launch.Target, launch, cancellationToken);

    private async Task ConnectAsync(ProcessMonitorTarget target, ProcessMonitorLaunch? launch, CancellationToken cancellationToken)
    {
        if (!await _lifecycle.WaitAsync(0, cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("会话正在切换状态。");
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsMonitoring) throw new InvalidOperationException("已有活动会话。");
            await _transport.DisconnectAsync().ConfigureAwait(false);
            lock (_recordLock) { SessionId = Guid.NewGuid(); _records.Clear(); EvictedRecords = 0; _target = target; }
            Status = "正在附加";
            await _transport.ConnectAsync(target, SessionId, cancellationToken, launch).ConfigureAwait(false);
            lock (_recordLock)
            {
                if (_target is null) throw new IOException(Status);
                IsMonitoring = true;
                Status = "正在监控";
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error) when (!IsMonitoring && !_disposed)
        {
            lock (_recordLock) _target = null;
            Status = error.Message;
            await _transport.DisconnectAsync().ConfigureAwait(false);
            StateChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }
        finally { _lifecycle.Release(); }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            lock (_recordLock) { _target = null; IsMonitoring = false; }
            await _transport.DisconnectAsync().ConfigureAwait(false);
            Status = "已停止；目标进程继续运行";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally { _lifecycle.Release(); }
    }

    private void OnRecord(object? sender, ProcessMonitorRecord record)
    {
        lock (_recordLock)
        {
            if (_target is null || record.SessionId != SessionId || record.ProcessId != _target.ProcessId
                || record.ProcessStartTime != _target.StartTime) return;
            if (_records.Count == _historyCapacity) { _records.Dequeue(); EvictedRecords++; }
            _records.Enqueue(record);
        }
        RecordReceived?.Invoke(this, record);
    }

    private void OnDisconnected(object? sender, string message)
    {
        lock (_recordLock) { _target = null; IsMonitoring = false; }
        Status = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            lock (_recordLock) { _target = null; IsMonitoring = false; }
            _transport.RecordReceived -= OnRecord;
            _transport.DisconnectedUnexpectedly -= OnDisconnected;
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }
}

public sealed record ProcessMonitorSnapshot(Guid SessionId, IReadOnlyList<ProcessMonitorRecord> Records, long EvictedRecords);
