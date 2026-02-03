using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services.Handlers;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class SequentialReplyHandlerTests
{
    private readonly SequentialReplyHandler _handler;

    public SequentialReplyHandlerTests()
    {
        _handler = new SequentialReplyHandler();
    }

    [Fact]
    public void Mode_ShouldReturnSequential()
    {
        _handler.Mode.Should().Be(ReplyMode.Sequential);
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
    public void Process_WithNoFrames_ShouldReturnNoReply()
    {
        // Arrange
        var config = new AutoReplyConfig();
        byte[] data = [0x01];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithDisabledFrame_ShouldSkipIt()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Content = "AA", IsHexMode = true, IsEnabled = false, SortOrder = 0 },
                    new SequentialFrame { Name = "Frame 2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
                ],
                CurrentIndex = 0
            }
        };
        byte[] data = [0x01];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xBB]);
        result.MatchedRuleName.Should().Be("Frame 2");
    }

    [Fact]
    public void Process_WithSingleFrame_ShouldReturnIt()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "Frame 1", Content = "01 02 03", IsHexMode = true, IsEnabled = true }
                ],
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0x01, 0x02, 0x03]);
        result.MatchedRuleName.Should().Be("Frame 1");
    }

    [Fact]
    public void Process_ShouldIncrementIndexAfterReply()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
                    new SequentialFrame { Name = "F2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
                ],
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act - 第一次调用
        var result1 = _handler.Process(data, config);

        // Assert - 第一次应返回 AA
        result1.ShouldReply.Should().BeTrue();
        result1.ResponseData.Should().Equal([0xAA]);
        config.SequentialConfig.CurrentIndex.Should().Be(1);

        // Act - 第二次调用
        var result2 = _handler.Process(data, config);

        // Assert - 第二次应返回 BB
        result2.ShouldReply.Should().BeTrue();
        result2.ResponseData.Should().Equal([0xBB]);
        config.SequentialConfig.CurrentIndex.Should().Be(2);
    }

    [Fact]
    public void Process_WithLoopEnabled_ShouldCycleBack()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
                    new SequentialFrame { Name = "F2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
                ],
                EnableLoop = true,
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act - 调用三次
        _handler.Process(data, config); // AA, index = 1
        _handler.Process(data, config); // BB, index = 2
        var result3 = _handler.Process(data, config); // 应循环回 AA

        // Assert
        result3.ShouldReply.Should().BeTrue();
        result3.ResponseData.Should().Equal([0xAA]);
        config.SequentialConfig.CurrentIndex.Should().Be(1);
    }

    [Fact]
    public void Process_WithLoopDisabled_ShouldStopAtEnd()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true }
                ],
                EnableLoop = false,
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act
        _handler.Process(data, config); // AA, index = 1
        var result2 = _handler.Process(data, config); // 应返回 NoReply

        // Assert
        result2.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithAsciiMode_ShouldReturnAsciiBytes()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "ASCII", Content = "OK", IsHexMode = false, IsEnabled = true }
                ],
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(System.Text.Encoding.ASCII.GetBytes("OK"));
    }

    [Fact]
    public void Reset_ShouldResetIndexToZero()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                CurrentIndex = 5
            }
        };

        // Act
        _handler.Reset(config);

        // Assert
        config.SequentialConfig.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void Process_ShouldRespectSortOrder()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "F3", Content = "CC", IsHexMode = true, IsEnabled = true, SortOrder = 2 },
                    new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
                    new SequentialFrame { Name = "F2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
                ],
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act - 应按 SortOrder 排序后依次返回
        var result1 = _handler.Process(data, config);
        var result2 = _handler.Process(data, config);
        var result3 = _handler.Process(data, config);

        // Assert
        result1.ResponseData.Should().Equal([0xAA]); // F1 (SortOrder=0)
        result2.ResponseData.Should().Equal([0xBB]); // F2 (SortOrder=1)
        result3.ResponseData.Should().Equal([0xCC]); // F3 (SortOrder=2)
    }

    [Fact]
    public void Process_WithEmptyContent_ShouldSkipFrame()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            SequentialConfig = new SequentialReplyConfig
            {
                Frames =
                [
                    new SequentialFrame { Name = "Empty", Content = "", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
                    new SequentialFrame { Name = "Valid", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
                ],
                CurrentIndex = 0
            }
        };
        byte[] data = [0xFF];

        // Act
        var result = _handler.Process(data, config);

        // Assert
        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xAA]);
        result.MatchedRuleName.Should().Be("Valid");
    }
}
