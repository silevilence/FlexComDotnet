using System.Buffers.Binary;
using System.Text;
using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

/// <summary>Decodes the native wire boundary and inserts loss markers in producer order.</summary>
public sealed class ProcessMonitorRecordStream(ProcessMonitorTarget target, Guid session)
{
    private const int PayloadCapacity = 1024;
    private readonly ProcessMonitorTarget _target = target;
    private readonly Guid _session = session;
    private uint _lastDropped;

    public IReadOnlyList<ProcessMonitorRecord> ReadBatch(IEnumerable<byte[]> records, uint totalDropped)
    {
        var result = new List<ProcessMonitorRecord>();
        foreach (var bytes in records)
        {
            if (bytes.Length != 2464) throw new IOException("采集记录长度错误。");
            var record = Decode(bytes);
            if (record.SessionId != _session) continue;
            AddGap(result, (uint)record.DroppedRecords);
            result.Add(record);
        }
        AddGap(result, totalDropped);
        return result;
    }

    private void AddGap(List<ProcessMonitorRecord> result, uint dropped)
    {
        if (dropped == _lastDropped) return;
        _lastDropped = dropped;
        result.Add(new() { SessionId = _session, ProcessId = _target.ProcessId,
            ProcessStartTime = _target.StartTime, Api = "采集缺口", Direction = CaptureDirection.Control,
            Integrity = CaptureIntegrity.Gap, Timestamp = DateTimeOffset.UtcNow, DroppedRecords = dropped });
    }
    private ProcessMonitorRecord Decode(byte[] bytes)
    {
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
        ulong U64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
        string Text(int offset, int length) => Encoding.Unicode.GetString(bytes, offset, length).TrimEnd('\0');
        var dataLength = U32(84); var attemptLength = U32(88);
        if (dataLength > PayloadCapacity || attemptLength > PayloadCapacity || U32(60) > 2 || U32(80) > 5) throw new IOException("采集记录格式错误。");
        if (dataLength > U32(68) || U32(72) == 0 && dataLength != 0)
            throw new IOException("采集载荷超过实际成功完成的长度。");
        string[] names = ["未知", "CreateFile", "CloseHandle", "ReadFile", "WriteFile", "ReadFileEx", "WriteFileEx", "SetCommState", "GetCommState", "SetCommTimeouts", "GetCommTimeouts", "EscapeCommFunction", "PurgeComm", "CancelIo", "采集缺口"];
        var api = U32(56);
        return new() { SessionId = new Guid(bytes.AsSpan(0, 16)), ProcessId = _target!.ProcessId, ProcessStartTime = _target.StartTime,
            Sequence = (long)U64(16), Handle = U64(24), HandleGeneration = (long)U64(32), RequestId = (long)U64(40),
            Timestamp = DateTimeOffset.FromFileTime((long)U64(48)), Api = api < names.Length ? names[api] : "未知 API",
            Direction = (CaptureDirection)U32(60), RequestedLength = U32(64), CompletedLength = U32(68), Succeeded = U32(72) != 0,
            ErrorCode = unchecked((int)U32(76)), Integrity = (CaptureIntegrity)U32(80), PortName = Text(92, 64), Detail = Text(156, 256),
            Data = bytes.AsSpan(412, (int)dataLength).ToArray(), WriteAttempt = bytes.AsSpan(1436, (int)attemptLength).ToArray(), DroppedRecords = U32(2460) };
    }

}
