namespace FlexComDotnet.Core.Features.Capture.Models;

public enum CaptureSource { KernelSerial, ProcessApi, Usb }
public enum CaptureDirection { Transmit, Receive, Control }
public enum CaptureTimeMeaning { KernelRecord, ApiCompletion, UsbCapture }
public enum CaptureIntegrity { Complete, Gap, Truncated, Failed, Disconnected, Unavailable }

/// <summary>Source-neutral processing projection; original source records are retained by the caller.</summary>
public sealed record CaptureRecord(CaptureSource Source, Guid SessionId, string StreamId,
    CaptureDirection Direction, long RecordId, DateTimeOffset Timestamp, CaptureTimeMeaning TimeMeaning,
    CaptureIntegrity Integrity, byte[] Data);

public sealed record CaptureRecordReference(CaptureSource Source, Guid SessionId, long RecordId);

public sealed record CaptureFrame(CaptureSource Source, Guid SessionId, string StreamId,
    CaptureDirection Direction, CaptureTimeMeaning TimeMeaning, DateTimeOffset FirstTimestamp,
    DateTimeOffset LastTimestamp, byte[] Data, IReadOnlyList<CaptureRecordReference> Records);
