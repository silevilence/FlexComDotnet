using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;
using MatchType = FlexComDotnet.Core.Features.AutoReply.Models.MatchType;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class AutoReplyServiceTests
{
    private readonly Mock<ISerialPortService> _mockSerialService;
    private readonly AutoReplyService _service;

    public AutoReplyServiceTests()
    {
        _mockSerialService = new Mock<ISerialPortService>();
        // 默认设置 Send 方法返回 true
        _mockSerialService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _service = new AutoReplyService(_mockSerialService.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        _service.Config.Should().NotBeNull();
        _service.IsRunning.Should().BeFalse();
        _service.ReceiveCount.Should().Be(0);
        _service.ReplyCount.Should().Be(0);
    }

    [Fact]
    public void GetAllHandlers_ShouldReturnAllHandlers()
    {
        // Act
        var handlers = _service.GetAllHandlers();

        // Assert
        handlers.Should().HaveCount(2);
        handlers.Should().Contain(h => h.Mode == ReplyMode.Match);
        handlers.Should().Contain(h => h.Mode == ReplyMode.Sequential);
    }

    [Fact]
    public void GetHandler_WithMatchMode_ShouldReturnMatchHandler()
    {
        // Act
        var handler = _service.GetHandler(ReplyMode.Match);

        // Assert
        handler.Mode.Should().Be(ReplyMode.Match);
    }

    [Fact]
    public void GetHandler_WithSequentialMode_ShouldReturnSequentialHandler()
    {
        // Act
        var handler = _service.GetHandler(ReplyMode.Sequential);

        // Assert
        handler.Mode.Should().Be(ReplyMode.Sequential);
    }

    [Fact]
    public void Start_ShouldSetIsRunningToTrue()
    {
        // Act
        _service.Start();

        // Assert
        _service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Stop_ShouldSetIsRunningToFalse()
    {
        // Arrange
        _service.Start();

        // Act
        _service.Stop();

        // Assert
        _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void UpdateConfig_ShouldUpdateConfigProperty()
    {
        // Arrange
        var newConfig = new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 500
        };

        // Act
        _service.UpdateConfig(newConfig);

        // Assert
        _service.Config.IsEnabled.Should().BeTrue();
        _service.Config.DebounceWindowMs.Should().Be(500);
    }

    [Fact]
    public void ResetCounters_ShouldResetBothCounters()
    {
        // Arrange - 配置一个匹配规则
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "AA",
                        ResponseContent = "BB"
                    }
                }
            ]
        });
        _service.Start();

        // Act
        _service.ResetCounters();

        // Assert
        _service.ReceiveCount.Should().Be(0);
        _service.ReplyCount.Should().Be(0);
    }

    [Fact]
    public void ResetHandlerState_ShouldResetSequentialIndex()
    {
        // Arrange
        var config = new AutoReplyConfig
        {
            Rules =
            [
                new AutoReplyRule
                {
                    Id = "seq-1",
                    Name = "SeqRule",
                    Type = ReplyMode.Sequential,
                    IsEnabled = true,
                    SequentialConfig = new SequentialRuleConfig
                    {
                        CurrentIndex = 5
                    }
                }
            ]
        };
        _service.UpdateConfig(config);

        // Act
        _service.ResetHandlerState();

        // Assert
        _service.Config.Rules[0].SequentialConfig!.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void WhenDataReceived_AndNotRunning_ShouldNotProcess()
    {
        // Arrange
        var replyTriggered = false;
        _service.ReplyTriggered += (_, _) => replyTriggered = true;

        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "AA",
                        ResponseContent = "BB"
                    }
                }
            ]
        });
        // 注意：不调用 Start()

        // Act - 模拟数据接收
        _mockSerialService.Raise(s => s.FrameReceived += null, _mockSerialService.Object, new byte[] { 0xAA });

        // Assert
        replyTriggered.Should().BeFalse();
        _service.ReceiveCount.Should().Be(0);
    }

    [Fact]
    public void WhenDataReceived_AndDisabled_ShouldNotProcess()
    {
        // Arrange
        var replyTriggered = false;
        _service.ReplyTriggered += (_, _) => replyTriggered = true;

        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = false, // 禁用
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "AA",
                        ResponseContent = "BB"
                    }
                }
            ]
        });
        _service.Start();

        // Act
        _mockSerialService.Raise(s => s.FrameReceived += null, _mockSerialService.Object, new byte[] { 0xAA });

        // Assert
        replyTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task WhenDataReceived_WithMatchingRule_ShouldTriggerReply()
    {
        // Arrange
        ReplyEventArgs? capturedArgs = null;
        var eventTriggered = new TaskCompletionSource<bool>();
        _service.ReplyTriggered += (_, args) =>
        {
            capturedArgs = args;
            eventTriggered.TrySetResult(true);
        };

        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 1, // 防抖窗口最小延迟
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "AA",
                        MatchType = MatchType.HexContains,
                        ResponseContent = "BB CC",
                        IsResponseHex = true
                    }
                }
            ]
        });
        _service.Start();

        // Act
        _mockSerialService.Raise(s => s.FrameReceived += null, _mockSerialService.Object, new byte[] { 0xAA });

        // 等待事件或超时
        var completed = await Task.WhenAny(eventTriggered.Task, Task.Delay(1000));

        // Assert
        _service.ReceiveCount.Should().Be(1);
        _service.ReplyCount.Should().Be(1);
        capturedArgs.Should().NotBeNull();
        capturedArgs!.ReplyData.Should().Equal([0xBB, 0xCC]);
        capturedArgs.RuleName.Should().Be("Test");
        _mockSerialService.Verify(s => s.Send(new byte[] { 0xBB, 0xCC }), Times.Once);
    }

    [Fact]
    public void WhenDataReceived_WithNoMatchingRule_ShouldNotReply()
    {
        // Arrange
        var replyTriggered = false;
        _service.ReplyTriggered += (_, _) => replyTriggered = true;

        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 1,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "FF",
                        ResponseContent = "BB"
                    }
                }
            ]
        });
        _service.Start();

        // Act
        _mockSerialService.Raise(s => s.FrameReceived += null, _mockSerialService.Object, new byte[] { 0xAA });

        Thread.Sleep(100);

        // Assert
        _service.ReceiveCount.Should().Be(1);
        _service.ReplyCount.Should().Be(0);
        replyTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task WhenDataReceived_WithSequentialRule_ShouldReplyInOrder()
    {
        // Arrange
        var replies = new List<byte[]>();
        var replyCount = 0;
        var eventTriggered = new TaskCompletionSource<bool>();
        _service.ReplyTriggered += (_, args) =>
        {
            replies.Add(args.ReplyData);
            replyCount++;
            if (replyCount >= 2)
            {
                eventTriggered.TrySetResult(true);
            }
        };

        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 1,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "SeqRule",
                    Type = ReplyMode.Sequential,
                    IsEnabled = true,
                    SequentialConfig = new SequentialRuleConfig
                    {
                        Frames =
                        [
                            new SequentialFrame { Content = "AA", IsHexMode = true, IsEnabled = true, SortOrder = 0 },
                            new SequentialFrame { Content = "BB", IsHexMode = true, IsEnabled = true, SortOrder = 1 }
                        ],
                        EnableLoop = false,
                        CurrentIndex = 0
                    }
                }
            ]
        });
        _service.Start();

        // Act
        _mockSerialService.Raise(s => s.FrameReceived += null, _mockSerialService.Object, new byte[] { 0x01 });
        await Task.Delay(100);
        _mockSerialService.Raise(s => s.FrameReceived += null, _mockSerialService.Object, new byte[] { 0x02 });

        // 等待事件或超时
        await Task.WhenAny(eventTriggered.Task, Task.Delay(1000));

        // Assert
        replies.Should().HaveCount(2);
        replies[0].Should().Equal([0xAA]);
        replies[1].Should().Equal([0xBB]);
    }

    [Fact]
    public void Dispose_ShouldStopAndUnsubscribe()
    {
        // Arrange
        _service.Start();

        // Act
        _service.Dispose();

        // Assert
        _service.IsRunning.Should().BeFalse();
    }
}
