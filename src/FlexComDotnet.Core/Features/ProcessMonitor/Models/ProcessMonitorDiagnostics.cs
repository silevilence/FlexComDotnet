using System.Buffers.Binary;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Models;

/// <summary>Cumulative observation loss, not business completions or notification counts.
/// Retirement saturation is a separate capability warning. Timing uses Windows QPC ticks.</summary>
public sealed record ProcessMonitorDiagnostics(
    uint SnapshotContention, uint PublishContention, uint CompletionContention, uint QueueFull,
    uint HandleCapacity, uint PendingCapacity, uint AmbiguousCompletion, uint RetirementCapacity,
    uint QueueHighWater, uint NotificationLoss, ulong PublishCalls, ulong PublishTicks, ulong MaxPublishTicks)
{
    public ulong LostObservations => (ulong)SnapshotContention + PublishContention + CompletionContention
        + QueueFull + HandleCapacity + PendingCapacity + AmbiguousCompletion;

    public static ProcessMonitorDiagnostics FromWire(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 64) throw new IOException("采集诊断布局不匹配。");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[24..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[32..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[36..]), BinaryPrimitives.ReadUInt64LittleEndian(bytes[40..]),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[48..]), BinaryPrimitives.ReadUInt64LittleEndian(bytes[56..]));
    }
}
