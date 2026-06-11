using FlexComDotnet.Core.Features.AutoReply.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class AutoReplyConfigTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new AutoReplyConfig();

        // Assert
        config.IsEnabled.Should().BeFalse();
        config.DebounceWindowMs.Should().Be(50);
        config.DecisionMode.Should().Be(DecisionMode.LAST);
        config.Rules.Should().NotBeNull();
        config.Rules.Should().BeEmpty();
    }

    [Fact]
    public void Rules_ShouldSupportMultipleTypes()
    {
        // Act
        var config = new AutoReplyConfig
        {
            Rules =
            [
                new AutoReplyRule { Name = "Match1", Type = ReplyMode.Match, MatchConfig = new MatchRuleConfig() },
                new AutoReplyRule { Name = "Seq1", Type = ReplyMode.Sequential, SequentialConfig = new SequentialRuleConfig() },
                new AutoReplyRule { Name = "Proto1", Type = ReplyMode.Protocol, ProtocolConfig = new ProtocolRuleConfig() }
            ]
        };

        // Assert
        config.Rules.Should().HaveCount(3);
        config.Rules[0].Type.Should().Be(ReplyMode.Match);
        config.Rules[1].Type.Should().Be(ReplyMode.Sequential);
        config.Rules[2].Type.Should().Be(ReplyMode.Protocol);
    }

    [Fact]
    public void SequentialRuleConfig_ShouldHaveDefaultValues()
    {
        // Act
        var seqConfig = new SequentialRuleConfig();

        // Assert
        seqConfig.Frames.Should().BeEmpty();
        seqConfig.EnableLoop.Should().BeTrue();
        seqConfig.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var frame = new SequentialFrame { Name = "Frame1" };

        // Act
        var config = new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 500,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Rule1",
                    Type = ReplyMode.Match,
                    MatchConfig = new MatchRuleConfig { TriggerPattern = "AA", ResponseContent = "BB" }
                },
                new AutoReplyRule
                {
                    Name = "SeqRule1",
                    Type = ReplyMode.Sequential,
                    SequentialConfig = new SequentialRuleConfig
                    {
                        Frames = [frame],
                        EnableLoop = false,
                        CurrentIndex = 2
                    }
                }
            ]
        };

        // Assert
        config.IsEnabled.Should().BeTrue();
        config.DebounceWindowMs.Should().Be(500);
        config.DecisionMode.Should().Be(DecisionMode.LAST);
        config.Rules.Should().HaveCount(2);
        config.Rules[0].Name.Should().Be("Rule1");
        config.Rules[1].SequentialConfig!.Frames.Should().HaveCount(1);
        config.Rules[1].SequentialConfig!.Frames[0].Name.Should().Be("Frame1");
        config.Rules[1].SequentialConfig!.EnableLoop.Should().BeFalse();
        config.Rules[1].SequentialConfig!.CurrentIndex.Should().Be(2);
    }
}
