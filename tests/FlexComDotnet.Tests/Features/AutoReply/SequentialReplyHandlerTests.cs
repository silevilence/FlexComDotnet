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

    private static AutoReplyRule CreateSequentialRule(List<SequentialFrame> frames, bool enableLoop = true, int currentIndex = 0, string name = "SeqRule")
    {
        return new AutoReplyRule
        {
            Name = name,
            Type = ReplyMode.Sequential,
            IsEnabled = true,
            SequentialConfig = new SequentialRuleConfig
            {
                Frames = frames,
                EnableLoop = enableLoop,
                CurrentIndex = currentIndex
            }
        };
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
        var rule = CreateSequentialRule([]);
        var result = _handler.Process([], rule);
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithNoFrames_ShouldReturnNoReply()
    {
        var rule = CreateSequentialRule([]);
        byte[] data = [0x01];
        var result = _handler.Process(data, rule);
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithDisabledFrame_ShouldSkipIt()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Content = "AA", IsHexMode = true, IsEnabled = false, SortOrder = 0 },
            new SequentialFrame { Name = "Frame 2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
        ]);
        byte[] data = [0x01];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xBB]);
    }

    [Fact]
    public void Process_WithSingleFrame_ShouldReturnIt()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "Frame 1", Content = "01 02 03", IsHexMode = true, IsEnabled = true }
        ]);
        byte[] data = [0xFF];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0x01, 0x02, 0x03]);
    }

    [Fact]
    public void Process_ShouldIncrementIndexAfterReply()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
            new SequentialFrame { Name = "F2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
        ]);
        byte[] data = [0xFF];

        var result1 = _handler.Process(data, rule);
        result1.ShouldReply.Should().BeTrue();
        result1.ResponseData.Should().Equal([0xAA]);
        rule.SequentialConfig!.CurrentIndex.Should().Be(1);

        var result2 = _handler.Process(data, rule);
        result2.ShouldReply.Should().BeTrue();
        result2.ResponseData.Should().Equal([0xBB]);
        rule.SequentialConfig!.CurrentIndex.Should().Be(2);
    }

    [Fact]
    public void Process_WithLoopEnabled_ShouldCycleBack()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
            new SequentialFrame { Name = "F2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
        ], enableLoop: true);
        byte[] data = [0xFF];

        _handler.Process(data, rule); // AA, index = 1
        _handler.Process(data, rule); // BB, index = 2
        var result3 = _handler.Process(data, rule); // 应循环回 AA

        result3.ShouldReply.Should().BeTrue();
        result3.ResponseData.Should().Equal([0xAA]);
        rule.SequentialConfig!.CurrentIndex.Should().Be(1);
    }

    [Fact]
    public void Process_WithLoopDisabled_ShouldStopAtEnd()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true }
        ], enableLoop: false);
        byte[] data = [0xFF];

        _handler.Process(data, rule); // AA, index = 1
        var result2 = _handler.Process(data, rule); // 应返回 NoReply

        result2.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithAsciiMode_ShouldReturnAsciiBytes()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "ASCII", Content = "OK", IsHexMode = false, IsEnabled = true }
        ]);
        byte[] data = [0xFF];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal(System.Text.Encoding.ASCII.GetBytes("OK"));
    }

    [Fact]
    public void Reset_ShouldResetIndexToZero()
    {
        var rule = CreateSequentialRule([], currentIndex: 5);

        _handler.Reset(rule);

        rule.SequentialConfig!.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void Process_ShouldRespectSortOrder()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "F3", Content = "CC", IsHexMode = true, IsEnabled = true, SortOrder = 2 },
            new SequentialFrame { Name = "F1", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
            new SequentialFrame { Name = "F2", Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
        ]);
        byte[] data = [0xFF];

        var result1 = _handler.Process(data, rule);
        var result2 = _handler.Process(data, rule);
        var result3 = _handler.Process(data, rule);

        result1.ResponseData.Should().Equal([0xAA]); // F1 (SortOrder=0)
        result2.ResponseData.Should().Equal([0xBB]); // F2 (SortOrder=1)
        result3.ResponseData.Should().Equal([0xCC]); // F3 (SortOrder=2)
    }

    [Fact]
    public void Process_WithEmptyContent_ShouldSkipFrame()
    {
        var rule = CreateSequentialRule(
        [
            new SequentialFrame { Name = "Empty", Content = "", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
            new SequentialFrame { Name = "Valid", Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
        ]);
        byte[] data = [0xFF];

        var result = _handler.Process(data, rule);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().Equal([0xAA]);
    }
}
