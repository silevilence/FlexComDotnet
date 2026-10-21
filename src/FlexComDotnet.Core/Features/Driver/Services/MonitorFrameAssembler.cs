namespace FlexComDotnet.Core.Features.Driver.Services;

/// <summary>Explicit fixed-length framing of completed I/O data, never inferred wire timing.</summary>
public sealed class MonitorFrameAssembler
{
    private readonly int _frameLength;
    private readonly Dictionary<(ulong, SerialMonitorDataDirection), List<byte>> _buffers = [];
    private ulong _lastSequence;

    public MonitorFrameAssembler(int frameLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frameLength, 65536);
        _frameLength = frameLength;
    }

    public void Reset()
    {
        _buffers.Clear();
        _lastSequence = 0;
    }

    public IReadOnlyList<SerialMonitorDataRecord> Append(SerialMonitorDataRecord record)
    {
        if (record.Sequence != 0 && _lastSequence != 0 && record.Sequence != _lastSequence + 1)
            _buffers.Clear();
        _lastSequence = record.Sequence;
        if (record.Status < 0 || record.DataUnavailable || (record.Flags & 4) != 0)
        {
            _buffers.Clear();
            return [record];
        }
        if (record.Kind != 0 || record.Direction == SerialMonitorDataDirection.Control) return [record];
        var key = (record.PortId, record.Direction);
        if (!_buffers.TryGetValue(key, out var pending)) _buffers[key] = pending = [];
        List<SerialMonitorDataRecord> frames = [];
        foreach (var value in record.Data)
        {
            pending.Add(value);
            if (pending.Count != _frameLength) continue;
            frames.Add(record with { Data = pending.ToArray(), IsFrame = true, RequestId = 0,
                Offset = 0, Flags = 0, RequestedLength = (uint)_frameLength, TransferredLength = (uint)_frameLength });
            pending.Clear();
        }
        return frames;
    }
}
