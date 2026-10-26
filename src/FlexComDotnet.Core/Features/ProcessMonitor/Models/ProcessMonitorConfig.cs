namespace FlexComDotnet.Core.Features.ProcessMonitor.Models;

/// <summary>Only user preferences are persisted; process IDs and live sessions are never restored.</summary>
public sealed class ProcessMonitorConfig
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string PortFilter { get; set; } = string.Empty;
}
