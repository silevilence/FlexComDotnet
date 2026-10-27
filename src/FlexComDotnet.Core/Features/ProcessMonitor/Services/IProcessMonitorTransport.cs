using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

public interface IProcessMonitorTransport : IAsyncDisposable
{
    event EventHandler<ProcessMonitorRecord>? RecordReceived;
    event EventHandler<string>? DisconnectedUnexpectedly;
    Task ConnectAsync(ProcessMonitorTarget target, Guid sessionId, CancellationToken cancellationToken, ProcessMonitorLaunch? launch = null);
    Task DisconnectAsync();
}
