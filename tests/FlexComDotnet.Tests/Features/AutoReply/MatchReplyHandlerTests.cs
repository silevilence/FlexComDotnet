using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services.Handlers;
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
}
