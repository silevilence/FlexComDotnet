using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.Capture.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Capture;

public class CaptureFrameAssemblerTests
{
    private static readonly Guid Session = Guid.NewGuid();
    private static CaptureRecord Record(long id, byte[] data, CaptureSource source = CaptureSource.ProcessApi,
        CaptureDirection direction = CaptureDirection.Receive, Guid? session = null, string stream = "pid:10/start:1/handle:4/gen:1") =>
        new(source, session ?? Session, stream, direction, id, DateTimeOffset.UnixEpoch.AddMilliseconds(id),
            CaptureTimeMeaning.ApiCompletion, CaptureIntegrity.Complete, data);

    [Fact]
    public void Append_ShouldIsolateSourcesSessionsStreamsAndDirectionsAndRetainProvenance()
    {
        var assembler = new CaptureFrameAssembler(3);
        assembler.Append(Record(1, [1, 2])).Should().BeEmpty();
        assembler.Append(Record(1, [11, 12], CaptureSource.Usb)).Should().BeEmpty();
        assembler.Append(Record(2, [21, 22], direction: CaptureDirection.Transmit)).Should().BeEmpty();
        assembler.Append(Record(1, [31, 32], session: Guid.NewGuid())).Should().BeEmpty();
        assembler.Append(Record(3, [41, 42], stream: "pid:10/start:1/handle:4/gen:2")).Should().BeEmpty();
        var frame = assembler.Append(Record(4, [3, 4, 5, 6])).First();
        frame.Data.Should().Equal(1, 2, 3);
        frame.Records.Should().Equal(new CaptureRecordReference(CaptureSource.ProcessApi, Session, 1),
            new CaptureRecordReference(CaptureSource.ProcessApi, Session, 4));
        frame.FirstTimestamp.Should().Be(DateTimeOffset.UnixEpoch.AddMilliseconds(1));
        frame.LastTimestamp.Should().Be(DateTimeOffset.UnixEpoch.AddMilliseconds(4));
    }

    [Theory]
    [InlineData(CaptureIntegrity.Gap)]
    [InlineData(CaptureIntegrity.Truncated)]
    [InlineData(CaptureIntegrity.Failed)]
    [InlineData(CaptureIntegrity.Disconnected)]
    [InlineData(CaptureIntegrity.Unavailable)]
    public void Append_WhenIntegrityIsBroken_ShouldDiscardPartialData(CaptureIntegrity integrity)
    {
        var assembler = new CaptureFrameAssembler(3);
        assembler.Append(Record(1, [1, 2]));
        assembler.Append(Record(2, [99, 98, 97]) with { Integrity = integrity }).Should().BeEmpty();
        assembler.Append(Record(3, [3])).Should().BeEmpty();
        assembler.Append(Record(4, [4, 5])).Single().Data.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void Append_WhenControlOrEmptyRecordArrives_ShouldNotProduceBusinessFrames()
    {
        var assembler = new CaptureFrameAssembler(2);
        assembler.Append(Record(1, [9, 9], direction: CaptureDirection.Control)).Should().BeEmpty();
        assembler.Append(Record(2, [])).Should().BeEmpty();
    }

    [Fact]
    public void Append_WhenStreamLimitIsReached_ShouldReportDiscardAndNeverReuseEvictedBytes()
    {
        var assembler = new CaptureFrameAssembler(3, maximumStreams: 1);
        assembler.Append(Record(1, [1, 2]));
        assembler.Append(Record(2, [9], stream: "another"));
        assembler.DiscardedPartialStreams.Should().Be(1);
        assembler.Append(Record(3, [3])).Should().BeEmpty();
        assembler.Append(Record(4, [4, 5])).Single().Data.Should().Equal(3, 4, 5);
    }
}
