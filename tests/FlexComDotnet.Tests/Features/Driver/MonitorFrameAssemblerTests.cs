using FlexComDotnet.Core.Features.Driver.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Driver;

public class MonitorFrameAssemblerTests
{
    private static SerialMonitorDataRecord Record(ulong sequence, params byte[] data) =>
        new(DateTime.Now, SerialMonitorDataDirection.Rx, data) { Sequence = sequence, PortId = 1 };

    [Fact]
    public void Append_ShouldReassembleAcrossReadBoundariesAndSplitMergedFrames()
    {
        var assembler = new MonitorFrameAssembler(3);
        assembler.Append(Record(1, 1, 2)).Should().BeEmpty();
        var frames = assembler.Append(Record(2, 3, 4, 5, 6, 7));
        frames.Should().HaveCount(2);
        frames[0].Data.Should().Equal(1, 2, 3);
        frames[1].Data.Should().Equal(4, 5, 6);
        frames.Should().OnlyContain(f => f.IsFrame);
    }

    [Fact]
    public void Append_WhenRecordsAreLost_ShouldNotJoinAcrossTheGap()
    {
        var assembler = new MonitorFrameAssembler(3);
        assembler.Append(Record(1, 1, 2));
        assembler.Append(Record(3, 3)).Should().BeEmpty();
        assembler.Append(Record(4, 4, 5)).Single().Data.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void Append_ShouldKeepPortsAndDirectionsSeparate()
    {
        var assembler = new MonitorFrameAssembler(2);
        assembler.Append(Record(1, 1));
        assembler.Append(Record(2, 2) with { Direction = SerialMonitorDataDirection.Tx }).Should().BeEmpty();
        assembler.Append(Record(3, 3) with { PortId = 2 }).Should().BeEmpty();
        assembler.Append(Record(4, 4)).Single().Data.Should().Equal(1, 4);
    }

    [Fact]
    public void Append_WhenDataUnavailableOrFailed_ShouldResetPartialFrame()
    {
        var assembler = new MonitorFrameAssembler(2);
        assembler.Append(Record(1, 1));
        assembler.Append(Record(2) with { Flags = 1, TransferredLength = 10 });
        assembler.Append(Record(3, 2)).Should().BeEmpty();
        assembler.Append(Record(4) with { Status = unchecked((int)0xC0000120) });
        assembler.Append(Record(5, 3)).Should().BeEmpty();
    }
}
