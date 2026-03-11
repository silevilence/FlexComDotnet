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

    private static AutoReplyRule CreateProtocolRule(string protocolName, Dictionary<string, string>? fieldValues = null, string name = "ProtoRule")
    {
        return new AutoReplyRule
        {
            Name = name,
            Type = ReplyMode.Protocol,
            IsEnabled = true,
            ProtocolConfig = new ProtocolRuleConfig
            {
                ProtocolName = protocolName,
                FieldValues = fieldValues ?? []
            }
        };
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
    public void Process_NoProtocolConfig_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var rule = new AutoReplyRule
        {
            Name = "No Config",
            Type = ReplyMode.Protocol,
            IsEnabled = true,
            ProtocolConfig = null
        };

        var result = handler.Process([0x01, 0x02], rule);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_EmptyProtocolName_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var rule = CreateProtocolRule("");

        var result = handler.Process([0x01, 0x02], rule);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_InvalidProtocolName_ReturnsNoReply()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);

        var rule = CreateProtocolRule("NonExistent");

        var result = handler.Process([0x01, 0x02], rule);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_ValidRule_BuildsAndReturnsFrame()
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

        var rule = CreateProtocolRule("SimpleProto",
            new() { ["Cmd"] = "1", ["Value"] = "42" },
            name: "Reply1");

        var result = handler.Process([0x01, 0x02], rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().NotBeEmpty();
        result.ResponseData[0].Should().Be(0xAA); // Header
        result.ResponseData[1].Should().Be(0x01); // Cmd
        result.ResponseData[2].Should().Be(42);   // Value
        result.MatchedRuleName.Should().Contain("Reply1");
    }

    [Fact]
    public void Reset_ShouldNotThrow()
    {
        var service = new ProtocolParserService(_checksumService);
        var handler = new ProtocolReplyHandler(service);
        var rule = CreateProtocolRule("Test");

        var action = () => handler.Reset(rule);

        action.Should().NotThrow();
    }
}
