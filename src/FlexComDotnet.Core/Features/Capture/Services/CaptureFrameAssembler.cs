using FlexComDotnet.Core.Features.Capture.Models;

namespace FlexComDotnet.Core.Features.Capture.Services;

/// <summary>One instance per consumer. Feed all records before applying display filters.</summary>
public sealed class CaptureFrameAssembler
{
    private readonly int _frameLength;
    private readonly int _maximumStreams;
    public long DiscardedPartialStreams { get; private set; }
    private readonly Dictionary<(CaptureSource, Guid, string, CaptureDirection, CaptureTimeMeaning), Pending> _pending = [];

    private sealed class Pending
    {
        public List<byte> Data { get; } = [];
        public List<CaptureRecordReference> Records { get; } = [];
        public DateTimeOffset FirstTimestamp { get; set; }
    }

    public CaptureFrameAssembler(int frameLength, int maximumStreams = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frameLength, 65536);
        _frameLength = frameLength;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStreams);
        _maximumStreams = maximumStreams;
    }

    public void Reset() => _pending.Clear();

    public IReadOnlyList<CaptureFrame> Append(CaptureRecord record)
    {
        if (record.Integrity != CaptureIntegrity.Complete)
        {
            BreakSession(record.Source, record.SessionId);
            return [];
        }
        if (record.Direction == CaptureDirection.Control || record.Data.Length == 0) return [];
        var key = (record.Source, record.SessionId, record.StreamId, record.Direction, record.TimeMeaning);
        if (!_pending.TryGetValue(key, out var pending))
        {
            if (_pending.Count >= _maximumStreams)
            {
                var evicted = _pending.First();
                if (evicted.Value.Data.Count > 0) DiscardedPartialStreams++;
                _pending.Remove(evicted.Key);
            }
            _pending[key] = pending = new();
        }
        List<CaptureFrame> frames = [];
        foreach (var value in record.Data)
        {
            if (pending.Data.Count == 0) pending.FirstTimestamp = record.Timestamp;
            var reference = new CaptureRecordReference(record.Source, record.SessionId, record.RecordId);
            if (pending.Records.Count == 0 || pending.Records[^1] != reference) pending.Records.Add(reference);
            pending.Data.Add(value);
            if (pending.Data.Count != _frameLength) continue;
            frames.Add(new(record.Source, record.SessionId, record.StreamId, record.Direction,
                record.TimeMeaning, pending.FirstTimestamp, record.Timestamp, pending.Data.ToArray(), pending.Records.ToArray()));
            pending.Data.Clear();
            pending.Records.Clear();
        }
        if (pending.Data.Count == 0) _pending.Remove(key);
        return frames;
    }

    public void BreakSession(CaptureSource source, Guid sessionId)
    {
        foreach (var key in _pending.Keys.Where(k => k.Item1 == source && k.Item2 == sessionId).ToArray())
            _pending.Remove(key);
    }
}
