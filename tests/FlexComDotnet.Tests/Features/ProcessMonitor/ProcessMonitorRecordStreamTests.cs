using System.Buffers.Binary;
using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.Capture.Services;
using FlexComDotnet.Core.Features.ProcessMonitor.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.ProcessMonitor;

public class ProcessMonitorRecordStreamTests
{
    [Fact]
    public void ReadBatch_ShouldBreakFramesAtTheActualLossPosition()
    {
        var session = Guid.NewGuid();
        var stream = new ProcessMonitorRecordStream(new(123, 987, "test", "x64"), session);
        var records = stream.ReadBatch([Wire(session, 1, 0, 1), Wire(session, 2, 1, 2), Wire(session, 3, 1, 3)], 1);
        records.Select(r => r.Integrity).Should().Equal(CaptureIntegrity.Complete, CaptureIntegrity.Gap,
            CaptureIntegrity.Complete, CaptureIntegrity.Complete);
        var assembler = new CaptureFrameAssembler(2);
        var frames = records.SelectMany(r => assembler.Append(r.ToProcessingRecord())).ToArray();
        frames.Should().ContainSingle().Which.Data.Should().Equal(2, 3);
        records.Last().DroppedRecords.Should().Be(1);
        stream.ReadBatch([], 2).Should().ContainSingle().Which.Integrity.Should().Be(CaptureIntegrity.Gap);
        stream.ReadBatch([], 2).Should().BeEmpty();
    }

    // Native little-endian wire contract, deliberately independent of the decoder's constants.
    [Fact]
    public void ReadBatch_WhenSuccessPayloadExceedsActualLength_ShouldRejectUninitializedBytes()
    {
        var session = Guid.NewGuid();
        var stream = new ProcessMonitorRecordStream(new(123, 987, "test", "x64"), session);
        var bytes = Wire(session, 1, 0, 0x42);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0);
        var read = () => stream.ReadBatch([bytes], 0);
        read.Should().Throw<IOException>();
    }

    private static byte[] Wire(Guid session, ulong sequence, uint dropped, byte value)
    {
        var bytes = new byte[2464];
        session.TryWriteBytes(bytes);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16), sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), 42);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(48), 133000000000000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 3); // ReadFile
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 1); // Receive
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 1);
        bytes[412] = value;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2460), dropped);
        return bytes;
    }
}
