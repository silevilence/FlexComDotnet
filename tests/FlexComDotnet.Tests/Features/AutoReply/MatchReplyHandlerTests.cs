using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services.Handlers;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FluentAssertions;
using MatchType = FlexComDotnet.Core.Features.AutoReply.Models.MatchType;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class MatchReplyHandlerTests
{
    private readonly MatchReplyHandler _handler;

    public MatchReplyHandlerTests()
    {
        _handler = new MatchReplyHandler();
    }

    private static AutoReplyRule CreateMatchRule(string trigger, string response, MatchType matchType = MatchType.HexContains,
        bool isResponseHex = true, bool isEnabled = true, string name = "Test Rule", int sortOrder = 0)
    {
        return new AutoReplyRule
        {
            Name = name,
            Type = ReplyMode.Match,
            IsEnabled = isEnabled,
            SortOrder = sortOrder,
            MatchConfig = new MatchRuleConfig
            {
                TriggerPattern = trigger,
                MatchType = matchType,
                ResponseContent = response,
                IsResponseHex = isResponseHex
            }
        };
    }

    [Fact]
    public void Mode_ShouldReturnMatch()
    {
        _handler.Mode.Should().Be(ReplyMode.Match);
    }

    [Fact]
    public void DisplayName_ShouldNotBeEmpty()
    {
        _handler.DisplayName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _handler.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Process_WithEmptyData_ShouldReturnNoReply()
    {
        var rule = CreateMatchRule("01 02", "AA BB");
        var result = _handler.Process([], rule);
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithNoMatchConfig_ShouldReturnNoReply()
    {
        var rule = new AutoReplyRule { Type = ReplyMode.Match, MatchConfig = null };
        byte[] data = [0x01, 0x02, 0x03];
        var result = _handler.Process(data, rule);
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithHexContainsMatch_ShouldReturnReply()
    {
        var rule = CreateMatchRule("02 03", "AA BB CC", name: "Test Rule");
        byte[] data = [0x01, 0x02, 0x03, 0x04];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xAA, 0xBB, 0xCC]);
        result.MatchedRuleName.Should().Be("Test Rule");
    }

    [Fact]
    public void Process_WithHexContainsNoMatch_ShouldReturnNoReply()
    {
        var rule = CreateMatchRule("FF EE", "AA BB");
        byte[] data = [0x01, 0x02, 0x03];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithHexExactMatch_ShouldReturnReply()
    {
        var rule = CreateMatchRule("01 02 03", "DD EE", MatchType.HexExact, name: "Exact Match");
        byte[] data = [0x01, 0x02, 0x03];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xDD, 0xEE]);
    }

    [Fact]
    public void Process_WithHexExactNoMatch_ShouldReturnNoReply()
    {
        var rule = CreateMatchRule("01 02 03", "DD EE", MatchType.HexExact);
        byte[] data = [0x01, 0x02, 0x03, 0x04];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithAsciiContainsMatch_ShouldReturnReply()
    {
        var rule = CreateMatchRule("HELLO", "OK", MatchType.AsciiContains, isResponseHex: false, name: "ASCII Rule");
        byte[] data = System.Text.Encoding.ASCII.GetBytes("Say HELLO World");

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(System.Text.Encoding.ASCII.GetBytes("OK"));
    }

    [Fact]
    public void Process_WithAsciiExactMatch_ShouldReturnReply()
    {
        var rule = CreateMatchRule("PING", "PONG", MatchType.AsciiExact, isResponseHex: false, name: "Exact ASCII");
        byte[] data = System.Text.Encoding.ASCII.GetBytes("PING");

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(System.Text.Encoding.ASCII.GetBytes("PONG"));
    }

    [Fact]
    public void Process_WithHexResponseFromAsciiTrigger_ShouldWork()
    {
        var rule = CreateMatchRule("TEST", "01 02 03", MatchType.AsciiContains, isResponseHex: true, name: "Mixed Rule");
        byte[] data = System.Text.Encoding.ASCII.GetBytes("TEST");

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0x01, 0x02, 0x03]);
    }

    [Fact]
    public void Reset_ShouldNotThrow()
    {
        var rule = CreateMatchRule("AA", "BB");
        var action = () => _handler.Reset(rule);
        action.Should().NotThrow();
    }

    #region 协议匹配测试

    [Fact]
    public void Process_WithProtocolParse_NoProtocolService_ShouldReturnNoReply()
    {
        // Handler created without protocol service
        var rule = new AutoReplyRule
        {
            Name = "Proto Rule",
            Type = ReplyMode.Match,
            IsEnabled = true,
            MatchConfig = new MatchRuleConfig
            {
                MatchType = MatchType.ProtocolParse,
                TriggerProtocolName = "TestProto",
                ResponseContent = "AA BB",
                IsResponseHex = true
            }
        };

        var result = _handler.Process([0x01, 0x02], rule);
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithProtocolParse_UnknownProtocol_ShouldReturnNoReply()
    {
        var checksumService = new ChecksumService();
        var protocolService = new ProtocolParserService(checksumService);
        var handler = new MatchReplyHandler(protocolService);

        var rule = new AutoReplyRule
        {
            Name = "Proto Rule",
            Type = ReplyMode.Match,
            IsEnabled = true,
            MatchConfig = new MatchRuleConfig
            {
                MatchType = MatchType.ProtocolParse,
                TriggerProtocolName = "NonExistent",
                ResponseContent = "AA BB",
                IsResponseHex = true
            }
        };

        var result = handler.Process([0x01, 0x02], rule);
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithProtocolParse_ValidMatch_ShouldReturnReply()
    {
        var checksumService = new ChecksumService();
        var protocolService = new ProtocolParserService(checksumService);
        protocolService.RegisterDefinition(new FrameDefinition
        {
            Name = "SimpleProto",
            Header = "AA",
            Trailer = "55",
            Fields =
            [
                new FieldDefinition { Name = "Data", DataType = DataType.UInt8, Length = 1, StartIndex = 0, IsEnabled = true }
            ]
        });

        var handler = new MatchReplyHandler(protocolService);

        var rule = new AutoReplyRule
        {
            Name = "Proto Rule",
            Type = ReplyMode.Match,
            IsEnabled = true,
            MatchConfig = new MatchRuleConfig
            {
                MatchType = MatchType.ProtocolParse,
                TriggerProtocolName = "SimpleProto",
                ResponseContent = "CC DD",
                IsResponseHex = true
            }
        };

        var result = handler.Process([0xAA, 0x01, 0x55], rule);
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xCC, 0xDD]);
    }

    [Fact]
    public void Process_WithProtocolParse_FieldAssertion_Equal_ShouldMatch()
    {
        var checksumService = new ChecksumService();
        var protocolService = new ProtocolParserService(checksumService);
        protocolService.RegisterDefinition(new FrameDefinition
        {
            Name = "CmdProto",
            Header = "AA",
            Trailer = "55",
            Fields =
            [
                new FieldDefinition { Name = "Cmd", DataType = DataType.UInt8, Length = 1, StartIndex = 0, IsEnabled = true }
            ]
        });

        var handler = new MatchReplyHandler(protocolService);

        var rule = new AutoReplyRule
        {
            Name = "Cmd Match",
            Type = ReplyMode.Match,
            IsEnabled = true,
            MatchConfig = new MatchRuleConfig
            {
                MatchType = MatchType.ProtocolParse,
                TriggerProtocolName = "CmdProto",
                FieldAssertions = [new FieldAssertion { FieldName = "Cmd", Operator = AssertionOperator.Equal, ExpectedValue = "3" }],
                ResponseContent = "OK",
                IsResponseHex = false
            }
        };

        // Cmd = 3
        var result = handler.Process([0xAA, 0x03, 0x55], rule);
        result.ShouldReply.Should().BeTrue();

        // Cmd = 5 (should not match)
        var result2 = handler.Process([0xAA, 0x05, 0x55], rule);
        result2.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithProtocolParse_ResponseBuildMode_ProtocolBuild_NoConfig_ShouldReturnNoReply()
    {
        var checksumService = new ChecksumService();
        var protocolService = new ProtocolParserService(checksumService);
        protocolService.RegisterDefinition(new FrameDefinition
        {
            Name = "TestProto",
            Header = "AA",
            Trailer = "55",
            Fields =
            [
                new FieldDefinition { Name = "Data", DataType = DataType.UInt8, Length = 1, StartIndex = 0, IsEnabled = true }
            ]
        });

        var handler = new MatchReplyHandler(protocolService);

        var rule = new AutoReplyRule
        {
            Name = "Build Rule",
            Type = ReplyMode.Match,
            IsEnabled = true,
            MatchConfig = new MatchRuleConfig
            {
                MatchType = MatchType.ProtocolParse,
                TriggerProtocolName = "TestProto",
                ResponseMode = ResponseBuildMode.ProtocolBuild,
                ProtocolResponse = null, // no config
                ResponseContent = ""
            }
        };

        var result = handler.Process([0xAA, 0x01, 0x55], rule);
        result.ShouldReply.Should().BeFalse();
    }

    #endregion
}
