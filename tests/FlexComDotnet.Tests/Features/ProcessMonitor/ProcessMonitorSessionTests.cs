using FlexComDotnet.Core.Features.ProcessMonitor.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Services;
using FluentAssertions;
using System.Collections.Concurrent;
using Moq;

namespace FlexComDotnet.Tests.Features.ProcessMonitor;

public class ProcessMonitorSessionTests
{
    [Fact]
    public async Task Attach_ShouldFinishWithoutRequiringTheCallingUiContext()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Mock<IProcessMonitorTransport>();
        transport.Setup(t => t.ConnectAsync(It.IsAny<ProcessMonitorTarget>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>(), It.IsAny<ProcessMonitorLaunch?>())).Returns(ready.Task);
        var session = new ProcessMonitorSession(transport.Object);
        var context = new PausedContext();
        var previous = SynchronizationContext.Current;
        Task connection;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            connection = session.AttachAsync(new(123, 987, "test", "x64"));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        ready.SetResult();
        try { await connection.WaitAsync(TimeSpan.FromSeconds(2)); session.IsMonitoring.Should().BeTrue(); }
        finally { context.Drain(); await session.DisposeAsync(); }
    }

    private sealed class PausedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));
        public void Drain() { while (_queue.TryDequeue(out var item)) item.Callback(item.State); }
    }

    [Fact]
    public async Task Dispose_ShouldBeRepeatableAndRejectNewSessions()
    {
        var session = new ProcessMonitorSession(new TestTransport());
        await session.AttachAsync(new(123, 987, "test", "x64"));
        await session.DisposeAsync();
        await session.DisposeAsync();
        session.IsMonitoring.Should().BeFalse();
        var restart = () => session.AttachAsync(new(123, 987, "test", "x64"));
        await restart.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task StartStop_ShouldRejectDuplicateStartAndLateRecordsWithoutTerminatingTarget()
    {
        var transport = new TestTransport();
        await using var session = new ProcessMonitorSession(transport);
        var target = new ProcessMonitorTarget(123, 987, "test", "x64");
        await session.AttachAsync(target);
        var oldSession = session.SessionId;
        session.IsMonitoring.Should().BeTrue();
        var duplicate = () => session.AttachAsync(target);
        await duplicate.Should().ThrowAsync<InvalidOperationException>();
        transport.Emit(new ProcessMonitorRecord { SessionId = oldSession, ProcessId = 123, ProcessStartTime = 987, Sequence = 1, Api = "ReadFile", Data = [1, 2] });
        session.Records.Should().HaveCount(1);
        await session.StopAsync();
        session.IsMonitoring.Should().BeFalse();
        transport.Disconnected.Should().BeTrue();
        await session.AttachAsync(target);
        transport.Emit(new ProcessMonitorRecord { SessionId = oldSession, ProcessId = 123, ProcessStartTime = 987, Sequence = 2, Api = "ReadFile", Data = [3] });
        session.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Attach_WhenNativeConnectionFails_ShouldCleanUpAndAllowRetry()
    {
        var transport = new TestTransport { Failure = new UnauthorizedAccessException("access denied") };
        await using var session = new ProcessMonitorSession(transport);
        var target = new ProcessMonitorTarget(123, 987, "test", "x64");
        var attach = () => session.AttachAsync(target);
        await attach.Should().ThrowAsync<UnauthorizedAccessException>();
        session.IsMonitoring.Should().BeFalse();
        transport.Disconnected.Should().BeTrue();
        session.Status.Should().Contain("access denied");
        transport.Failure = null;
        await session.AttachAsync(target);
        session.IsMonitoring.Should().BeTrue();
    }

    [Fact]
    public async Task Attach_WhenTargetExitsDuringConnection_ShouldNotReportRunning()
    {
        var transport = new TestTransport { DisconnectDuringConnect = true };
        await using var session = new ProcessMonitorSession(transport);
        var attach = () => session.AttachAsync(new(123, 987, "test", "x64"));
        await attach.Should().ThrowAsync<IOException>();
        session.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public async Task Capture_WhenHistoryIsFull_ShouldBoundMemoryAndExposeEvictions()
    {
        var transport = new TestTransport();
        await using var session = new ProcessMonitorSession(transport, historyCapacity: 2);
        await session.AttachAsync(new(123, 987, "test", "x64"));
        for (var sequence = 1; sequence <= 3; sequence++)
            transport.Emit(new() { SessionId = session.SessionId, ProcessId = 123, ProcessStartTime = 987, Sequence = sequence });
        session.Records.Select(r => r.Sequence).Should().Equal(2, 3);
        session.EvictedRecords.Should().Be(1);
    }

    // Native transport is the OS/process boundary, not an internal business collaborator.
    private sealed class TestTransport : IProcessMonitorTransport
    {
        public event EventHandler<ProcessMonitorRecord>? RecordReceived;
        public event EventHandler<string>? DisconnectedUnexpectedly;
        public bool DisconnectDuringConnect { get; set; }
        public bool Disconnected { get; private set; }
        public Exception? Failure { get; set; }
        public Task ConnectAsync(ProcessMonitorTarget target, Guid sessionId, CancellationToken cancellationToken, ProcessMonitorLaunch? launch = null)
        {
            Disconnected = false;
            if (DisconnectDuringConnect) DisconnectedUnexpectedly?.Invoke(this, "目标已退出");
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
        public Task DisconnectAsync() { Disconnected = true; return Task.CompletedTask; }
        public void Emit(ProcessMonitorRecord record) => RecordReceived?.Invoke(this, record);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
