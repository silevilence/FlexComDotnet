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
        config.GlobalDelayMs.Should().Be(100);
        config.ActiveMode.Should().Be(ReplyMode.Match);
        config.MatchConfig.Should().NotBeNull();
        config.SequentialConfig.Should().NotBeNull();
    }

    [Fact]
    public void MatchConfig_ShouldHaveEmptyRulesByDefault()
    {
        // Act
        var config = new AutoReplyConfig();

        // Assert
        config.MatchConfig.Rules.Should().BeEmpty();
    }

    [Fact]
    public void SequentialConfig_ShouldHaveDefaultValues()
    {
        // Act
        var config = new AutoReplyConfig();

        // Assert
        config.SequentialConfig.Frames.Should().BeEmpty();
        config.SequentialConfig.EnableLoop.Should().BeTrue();
        config.SequentialConfig.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var matchRule = new MatchRule { Name = "Rule1" };
        var frame = new SequentialFrame { Name = "Frame1" };

        // Act
        var config = new AutoReplyConfig
        {
            IsEnabled = true,
            GlobalDelayMs = 500,
            ActiveMode = ReplyMode.Sequential,
            MatchConfig = new MatchReplyConfig { Rules = [matchRule] },
            SequentialConfig = new SequentialReplyConfig
            {
                Frames = [frame],
                EnableLoop = false,
                CurrentIndex = 2
            }
        };

        // Assert
        config.IsEnabled.Should().BeTrue();
        config.GlobalDelayMs.Should().Be(500);
        config.ActiveMode.Should().Be(ReplyMode.Sequential);
        config.MatchConfig.Rules.Should().HaveCount(1);
        config.MatchConfig.Rules[0].Name.Should().Be("Rule1");
        config.SequentialConfig.Frames.Should().HaveCount(1);
        config.SequentialConfig.Frames[0].Name.Should().Be("Frame1");
        config.SequentialConfig.EnableLoop.Should().BeFalse();
        config.SequentialConfig.CurrentIndex.Should().Be(2);
    }
}
