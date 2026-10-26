using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.Capture.Services;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Capture;

public class CaptureProcessingTests
{
    [Fact]
    public void ExportImport_ShouldPreserveBytesOriginTimeAndIntegrityForParsing()
    {
        var parsers = new ProtocolParserService(new ChecksumService());
        parsers.RegisterDefinition(new FrameDefinition { Name = "Modbus-RTU", ProtocolType = ProtocolType.ModbusRtu });
        var processor = new CaptureProcessing(parsers);
        var session = Guid.NewGuid();
        var record = new CaptureRecord(CaptureSource.Usb, session, "bus1/dev2/if0/ep81,\"raw\"",
            CaptureDirection.Receive, 42, DateTimeOffset.Parse("2026-10-26T08:00:00.1234567+08:00"),
            CaptureTimeMeaning.UsbCapture, CaptureIntegrity.Complete, [1, 3, 2, 0, 10, 0x38, 0x43]);
        var restored = processor.ImportJson(processor.ExportJson([record])).Single();
        restored.Should().BeEquivalentTo(record);
        processor.Format(restored, true).Should().Be("01 03 02 00 0A 38 43");
        processor.Matches(restored, "0a 38").Should().BeTrue();
        processor.Parse(restored, "Modbus-RTU").RawData.Should().Equal(record.Data);
        var parsed = processor.Parse(restored, "Modbus-RTU");
        parsed.IsValid.Should().BeTrue(parsed.ErrorMessage);
        processor.Parse(restored with { Source = CaptureSource.ProcessApi }, "Modbus-RTU")
            .Should().BeEquivalentTo(processor.Parse(restored, "Modbus-RTU"), options => options.Excluding(x => x.ParsedAt));
        var invalid = restored with { Integrity = CaptureIntegrity.Truncated };
        var parseInvalid = () => processor.Parse(invalid, "Modbus-RTU");
        parseInvalid.Should().Throw<InvalidOperationException>();
    }
}
