using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services.Handlers;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class ProtocolReplyHandlerTests
{
    private readonly IChecksumService _checksumService = new ChecksumService();

    private IProtocolParserService CreateServiceWithDefinition(FrameDefinition definition)
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(definition);
        return service;
    }

    [Fact]
    public void Mode_ShouldBeProtocol()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        handler.Mode.Should().Be(ReplyMode.Protocol);
    }

    [Fact]
    public void DisplayName_ShouldNotBeEmpty()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        handler.DisplayName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Process_NoSchemes_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig()
        };

        var result = handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_NoActiveScheme_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig
            {
                Schemes = [new ProtocolReplyScheme { Name = "Test", ProtocolName = "TestProto" }],
                ActiveSchemeIndex = -1
            }
        };

        var result = handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_InvalidProtocolName_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig
            {
                Schemes = [new ProtocolReplyScheme
                {
                    Name = "Test",
                    ProtocolName = "NonExistent",
                    IsEnabled = true
                }],
                ActiveSchemeIndex = 0
            }
        };

        var result = handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_DisabledScheme_ReturnsNoReply()
    {
        var definition = new FrameDefinition
        {
            Name = "TestProto",
            MinFrameLength = 2,
            Fields =
            [
                new FieldDefinition { Name = "Data", StartIndex = 0, DataType = DataType.UInt8, Length = 1 }
            ]
        };
        var service = CreateServiceWithDefinition(definition);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig
            {
                Schemes = [new ProtocolReplyScheme
                {
                    Name = "Test",
                    ProtocolName = "TestProto",
                    IsEnabled = false,
                    FieldValues = new() { ["Data"] = "42" }
                }],
                ActiveSchemeIndex = 0
            }
        };

        var result = handler.Process([0x01], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_ValidScheme_BuildsAndReturnsFrame()
    {
        var definition = new FrameDefinition
        {
            Name = "SimpleProto",
            Header = "AA",
            MinFrameLength = 3,
            Fields =
            [
                new FieldDefinition { Name = "Cmd", StartIndex = 0, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Value", StartIndex = 1, DataType = DataType.UInt8, Length = 1 }
            ]
        };
        var service = CreateServiceWithDefinition(definition);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig
            {
                Schemes = [new ProtocolReplyScheme
                {
                    Name = "Reply1",
                    ProtocolName = "SimpleProto",
                    IsEnabled = true,
                    FieldValues = new() { ["Cmd"] = "1", ["Value"] = "42" }
                }],
                ActiveSchemeIndex = 0
            }
        };

        var result = handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().NotBeEmpty();
        result.ResponseData[0].Should().Be(0xAA); // Header
        result.ResponseData[1].Should().Be(0x01); // Cmd
        result.ResponseData[2].Should().Be(42);   // Value
        result.MatchedRuleName.Should().Contain("Reply1");
    }

    [Fact]
    public void Process_WithMultipleSchemes_UsesActive()
    {
        var definition = new FrameDefinition
        {
            Name = "MultiProto",
            MinFrameLength = 1,
            Fields =
            [
                new FieldDefinition { Name = "Data", StartIndex = 0, DataType = DataType.UInt8, Length = 1 }
            ]
        };
        var service = CreateServiceWithDefinition(definition);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig
            {
                Schemes =
                [
                    new ProtocolReplyScheme
                    {
                        Name = "Scheme1",
                        ProtocolName = "MultiProto",
                        IsEnabled = true,
                        FieldValues = new() { ["Data"] = "10" }
                    },
                    new ProtocolReplyScheme
                    {
                        Name = "Scheme2",
                        ProtocolName = "MultiProto",
                        IsEnabled = true,
                        FieldValues = new() { ["Data"] = "20" }
                    }
                ],
                ActiveSchemeIndex = 1 // Second scheme active
            }
        };

        var result = handler.Process([0x01], config);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData[0].Should().Be(20);
        result.MatchedRuleName.Should().Contain("Scheme2");
    }

    [Fact]
    public void Process_ActiveSchemeIndexOutOfRange_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var config = new AutoReplyConfig
        {
            ActiveMode = ReplyMode.Protocol,
            ProtocolConfig = new ProtocolReplyConfig
            {
                Schemes = [new ProtocolReplyScheme { Name = "Test", ProtocolName = "X", IsEnabled = true }],
                ActiveSchemeIndex = 5
            }
        };

        var result = handler.Process([0x01], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Reset_ShouldNotThrow()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);
        var config = new AutoReplyConfig();

        var action = () => handler.Reset(config);

        action.Should().NotThrow();
    }
}
