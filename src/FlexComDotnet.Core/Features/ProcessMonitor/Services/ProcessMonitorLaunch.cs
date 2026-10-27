using System.Diagnostics;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

/// <summary>Owns only the helper and a child that has not entered application code yet.</summary>
public sealed class ProcessMonitorLaunch(Process helper, ProcessMonitorTarget target) : IAsyncDisposable
{
    public ProcessMonitorTarget Target { get; } = target;
    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        await helper.StandardInput.WriteLineAsync("START".AsMemory(), cancellationToken).ConfigureAwait(false);
        helper.StandardInput.Close();
        var error = helper.StandardError.ReadToEndAsync(cancellationToken);
        await helper.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        if (helper.ExitCode != 0) throw new IOException(await error.ConfigureAwait(false));
    }
    public async ValueTask DisposeAsync()
    {
        // EOF asks the helper to discard only its own still-suspended child. Activated targets are never terminated.
        if (!helper.HasExited) { helper.StandardInput.Close(); await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false); }
        helper.Dispose();
    }
}
