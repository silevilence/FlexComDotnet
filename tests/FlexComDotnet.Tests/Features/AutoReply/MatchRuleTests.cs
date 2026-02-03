using FlexComDotnet.Core.Features.AutoReply.Models;
using FluentAssertions;
using MatchType = FlexComDotnet.Core.Features.AutoReply.Models.MatchType;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class MatchRuleTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var rule = new MatchRule();

        // Assert
        rule.Id.Should().Be(0);
        rule.Name.Should().BeEmpty();
        rule.TriggerPattern.Should().BeEmpty();
        rule.MatchType.Should().Be(MatchType.HexContains);
        rule.ResponseContent.Should().BeEmpty();
        rule.IsResponseHex.Should().BeTrue();
        rule.IsEnabled.Should().BeTrue();
        rule.SortOrder.Should().Be(0);
        rule.Description.Should().BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange & Act
        var rule = new MatchRule
        {
            Id = 1,
            Name = "Test Rule",
            TriggerPattern = "AA BB CC",
            MatchType = MatchType.AsciiContains,
            ResponseContent = "DD EE FF",
            IsResponseHex = false,
            IsEnabled = false,
            SortOrder = 5,
            Description = "Test Description"
        };

        // Assert
        rule.Id.Should().Be(1);
        rule.Name.Should().Be("Test Rule");
        rule.TriggerPattern.Should().Be("AA BB CC");
        rule.MatchType.Should().Be(MatchType.AsciiContains);
        rule.ResponseContent.Should().Be("DD EE FF");
        rule.IsResponseHex.Should().BeFalse();
        rule.IsEnabled.Should().BeFalse();
        rule.SortOrder.Should().Be(5);
        rule.Description.Should().Be("Test Description");
    }
}
