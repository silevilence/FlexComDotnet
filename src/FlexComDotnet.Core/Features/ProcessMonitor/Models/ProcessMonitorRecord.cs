using FlexComDotnet.Core.Features.Capture.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Models;

public sealed record ProcessMonitorTarget(int ProcessId, long StartTime, string Name, string Architecture);

/// <summary>Original API observation; lengths and write attempts are retained separately.</summary>
public sealed record ProcessMonitorRecord
{
    public Guid SessionId { get; init; }
    public int ProcessId { get; init; }
    public long ProcessStartTime { get; init; }
    public long Sequence { get; init; }
    public ulong Handle { get; init; }
    public long HandleGeneration { get; init; }
    public long RequestId { get; init; }
    public string PortName { get; init; } = string.Empty;
    public string Api { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public CaptureDirection Direction { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public uint RequestedLength { get; init; }
    public uint CompletedLength { get; init; }
    public bool Succeeded { get; init; }
    public int ErrorCode { get; init; }
    public CaptureIntegrity Integrity { get; init; }
    public byte[] Data { get; init; } = [];
    public byte[] WriteAttempt { get; init; } = [];
    public long DroppedRecords { get; init; }

    public CaptureRecord ToProcessingRecord() => new(CaptureSource.ProcessApi, SessionId,
        $"{ProcessId}/{ProcessStartTime}/{Handle}/{HandleGeneration}", Direction, Sequence,
        Timestamp, CaptureTimeMeaning.ApiCompletion, Integrity, Data);
}
