using FlexComDotnet.Core.Features.AutoReply.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class ReplyResultTests
{
    [Fact]
    public void NoReply_ShouldReturnCorrectResult()
    {
        // Act
        var result = ReplyResult.NoReply;

        // Assert
        result.ShouldReply.Should().BeFalse();
        result.ResponseData.Should().BeEmpty();
        result.MatchedRuleName.Should().BeNull();
    }

    [Fact]
    public void Reply_WithData_ShouldReturnCorrectResult()
    {
        // Arrange
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        var result = ReplyResult.Reply(data);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(data);
        result.MatchedRuleName.Should().BeNull();
    }

    [Fact]
    public void Reply_WithDataAndRuleName_ShouldReturnCorrectResult()
    {
        // Arrange
        byte[] data = [0xAA, 0xBB];
        const string ruleName = "Test Rule";

        // Act
        var result = ReplyResult.Reply(data, ruleName);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(data);
        result.MatchedRuleName.Should().Be(ruleName);
    }
}
