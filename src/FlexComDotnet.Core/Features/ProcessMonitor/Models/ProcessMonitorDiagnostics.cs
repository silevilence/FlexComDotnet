using System.Buffers.Binary;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Models;

/// <summary>Cumulative observation loss, not business completions or notification counts.
/// Retirement saturation is a separate capability warning. Timing uses Windows QPC ticks.</summary>
public sealed record ProcessMonitorDiagnostics(
    uint SnapshotContention, uint PublishContention, uint CompletionContention, uint QueueFull,
    uint HandleCapacity, uint PendingCapacity, uint AmbiguousCompletion, uint RetirementCapacity,
    uint QueueHighWater, uint NotificationLoss, ulong PublishCalls, ulong PublishTicks, ulong MaxPublishTicks)
{
    public const int WireSize = 104;
    private const int SnapshotOffset = 0, PublishOffset = 8, CompletionOffset = 16, QueueFullOffset = 24;
    private const int HandleOffset = 32, PendingOffset = 40, AmbiguousOffset = 48, RetirementOffset = 56;
    private const int HighWaterOffset = 64, NotificationOffset = 72, CallsOffset = 80, TicksOffset = 88, MaxTicksOffset = 96;
    public ulong LostObservations => (ulong)SnapshotContention + PublishContention + CompletionContention
        + QueueFull + HandleCapacity + PendingCapacity + AmbiguousCompletion;

    public static ProcessMonitorDiagnostics FromWire(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != WireSize) throw new IOException("采集诊断布局不匹配。");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(bytes[SnapshotOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[PublishOffset..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[CompletionOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[QueueFullOffset..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[HandleOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[PendingOffset..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[AmbiguousOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[RetirementOffset..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[HighWaterOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[NotificationOffset..]), BinaryPrimitives.ReadUInt64LittleEndian(bytes[CallsOffset..]),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[TicksOffset..]), BinaryPrimitives.ReadUInt64LittleEndian(bytes[MaxTicksOffset..]));
    }
}
