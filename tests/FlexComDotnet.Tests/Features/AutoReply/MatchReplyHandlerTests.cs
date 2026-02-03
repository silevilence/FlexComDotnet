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
        // Arrange
        var config = new AutoReplyConfig();

        // Act
        var result = _handler.Process([], config);

        // Assert
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithNoRules_ShouldReturnNoReply()
    {
        // Arrange
        var config = new AutoReplyConfig();
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithDisabledRule_ShouldReturnNoReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        TriggerPattern = "01 02",
                        ResponseContent = "AA BB",
                        IsEnabled = false
                    }
                ]
            }
        };
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithHexContainsMatch_ShouldReturnReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        Name = "Test Rule",
                        TriggerPattern = "02 03",
                        MatchType = MatchType.HexContains,
                        ResponseContent = "AA BB CC",
                        IsResponseHex = true,
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = [0x01, 0x02, 0x03, 0x04];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xAA, 0xBB, 0xCC]);
        result.MatchedRuleName.Should().Be("Test Rule");
    }

    [Fact]
    public void Process_WithHexContainsNoMatch_ShouldReturnNoReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        TriggerPattern = "FF EE",
                        MatchType = MatchType.HexContains,
                        ResponseContent = "AA BB",
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithHexExactMatch_ShouldReturnReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        Name = "Exact Match",
                        TriggerPattern = "01 02 03",
                        MatchType = MatchType.HexExact,
                        ResponseContent = "DD EE",
                        IsResponseHex = true,
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xDD, 0xEE]);
    }

    [Fact]
    public void Process_WithHexExactNoMatch_ShouldReturnNoReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        TriggerPattern = "01 02 03",
                        MatchType = MatchType.HexExact,
                        ResponseContent = "DD EE",
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = [0x01, 0x02, 0x03, 0x04]; // 多了一个字节

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithAsciiContainsMatch_ShouldReturnReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        Name = "ASCII Rule",
                        TriggerPattern = "HELLO",
                        MatchType = MatchType.AsciiContains,
                        ResponseContent = "OK",
                        IsResponseHex = false,
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = System.Text.Encoding.ASCII.GetBytes("Say HELLO World");

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(System.Text.Encoding.ASCII.GetBytes("OK"));
    }

    [Fact]
    public void Process_WithAsciiExactMatch_ShouldReturnReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        Name = "Exact ASCII",
                        TriggerPattern = "PING",
                        MatchType = MatchType.AsciiExact,
                        ResponseContent = "PONG",
                        IsResponseHex = false,
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = System.Text.Encoding.ASCII.GetBytes("PING");

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(System.Text.Encoding.ASCII.GetBytes("PONG"));
    }

    [Fact]
    public void Process_WithMultipleRules_ShouldMatchFirstEnabled()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        Name = "Rule 1",
                        TriggerPattern = "01",
                        MatchType = MatchType.HexContains,
                        ResponseContent = "AA",
                        IsResponseHex = true,
                        IsEnabled = true,
                        SortOrder = 0
                    },
                    new MatchRule
                    {
                        Name = "Rule 2",
                        TriggerPattern = "01",
                        MatchType = MatchType.HexContains,
                        ResponseContent = "BB",
                        IsResponseHex = true,
                        IsEnabled = true,
                        SortOrder = 1
                    }
                ]
            }
        };
        byte[] data = [0x01, 0x02];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xAA]);
        result.MatchedRuleName.Should().Be("Rule 1");
    }

    [Fact]
    public void Process_WithHexResponseFromAsciiTrigger_ShouldWork()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            MatchConfig = new MatchReplyConfig
            {
                Rules =
                [
                    new MatchRule
                    {
                        Name = "Mixed Rule",
                        TriggerPattern = "TEST",
                        MatchType = MatchType.AsciiContains,
                        ResponseContent = "01 02 03",
                        IsResponseHex = true,
                        IsEnabled = true
                    }
                ]
            }
        };
        byte[] data = System.Text.Encoding.ASCII.GetBytes("TEST");

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0x01, 0x02, 0x03]);
    }

    [Fact]
    public void Reset_ShouldNotThrow()
    {
        // Arrange
        var config = new AutoReplyConfig();

        // Act & Assert
        var action = () => _handler.Reset(config);
        action.Should().NotThrow();
    }
}
