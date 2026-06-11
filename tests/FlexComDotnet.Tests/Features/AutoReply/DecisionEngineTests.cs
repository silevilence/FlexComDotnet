using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;
using MatchType = FlexComDotnet.Core.Features.AutoReply.Models.MatchType;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class DecisionEngineTests
{
    private readonly Mock<ISerialPortService> _mockSerial;
    private readonly AutoReplyService _service;
    private readonly List<byte[]> _sentReplies = [];

    public DecisionEngineTests()
    {
        _mockSerial = new Mock<ISerialPortService>();
        _mockSerial.Setup(s => s.Send(It.IsAny<byte[]>()))
            .Callback<byte[]>(data => _sentReplies.Add(data))
            .Returns(true);
        _service = new AutoReplyService(_mockSerial.Object);
    }

    [Fact]
    public void LAST_Mode_LastFrameMatches_ShouldReply()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.LAST,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    SortOrder = 0,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "BB",
                        ResponseContent = "CC",
                        MatchType = MatchType.HexContains
                    }
                }
            ]
        });
        _service.Start();

        var frame1 = new byte[] { 0xAA }; // 不匹配
        var frame2 = new byte[] { 0xBB }; // 匹配

        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame1);
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame2);
        Thread.Sleep(100);

        _sentReplies.Should().HaveCount(1);
        _sentReplies[0].Should().Equal(0xCC);
    }

    [Fact]
    public void LAST_Mode_LastFrameNoMatch_ShouldNotReply()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.LAST,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    SortOrder = 0,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "BB",
                        ResponseContent = "CC",
                        MatchType = MatchType.HexContains
                    }
                }
            ]
        });
        _service.Start();

        var frame1 = new byte[] { 0xBB }; // 匹配
        var frame2 = new byte[] { 0xAA }; // 不匹配（最后到达）

        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame1);
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame2);
        Thread.Sleep(100);

        // LAST 模式只检查最后一帧（0xAA），不匹配 → 不回复
        _sentReplies.Should().BeEmpty();
    }

    [Fact]
    public void OR_Mode_AnyFrameMatches_ShouldReply()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.OR,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    SortOrder = 0,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "BB",
                        ResponseContent = "CC",
                        MatchType = MatchType.HexContains
                    }
                }
            ]
        });
        _service.Start();

        var frame1 = new byte[] { 0xAA }; // 不匹配
        var frame2 = new byte[] { 0xBB }; // 匹配

        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame1);
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame2);
        Thread.Sleep(100);

        // OR 模式：任一帧匹配即可
        _sentReplies.Should().HaveCount(1);
    }

    [Fact]
    public void AND_Mode_AllFramesMustMatch()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.AND,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    SortOrder = 0,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "AA",
                        ResponseContent = "BB",
                        MatchType = MatchType.HexContains
                    }
                }
            ]
        });
        _service.Start();

        var frame1 = new byte[] { 0xAA }; // 匹配
        var frame2 = new byte[] { 0xAA }; // 匹配

        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame1);
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame2);
        Thread.Sleep(100);

        // AND 模式：所有帧必须全部匹配
        _sentReplies.Should().HaveCount(1);
    }

    [Fact]
    public void AND_Mode_AnyFrameNoMatch_ShouldNotReply()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.AND,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    SortOrder = 0,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "AA",
                        ResponseContent = "BB",
                        MatchType = MatchType.HexContains
                    }
                }
            ]
        });
        _service.Start();

        var frame1 = new byte[] { 0xAA }; // 匹配
        var frame2 = new byte[] { 0xBB }; // 不匹配

        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame1);
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame2);
        Thread.Sleep(100);

        // AND 模式：frame2 不匹配 → 不回复
        _sentReplies.Should().BeEmpty();
    }

    [Fact]
    public void Sequential_ShouldExecuteRegardlessOfDecisionMode()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.AND, // AND 模式，但无匹配规则
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Seq",
                    Type = ReplyMode.Sequential,
                    IsEnabled = true,
                    SortOrder = 0,
                    SequentialConfig = new SequentialRuleConfig
                    {
                        EnableLoop = false,
                        Frames =
                        [
                            new SequentialFrame { Content = "FF" }
                        ]
                    }
                }
            ]
        });
        _service.Start();

        var frame = new byte[] { 0xAA };
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame);
        Thread.Sleep(100);

        // 顺序回复不受决策模式约束
        _sentReplies.Should().HaveCount(1);
    }

    [Fact]
    public void FIRST_Mode_OnlyFirstFrameChecked()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.FIRST,
            Rules =
            [
                new AutoReplyRule
                {
                    Name = "Test",
                    Type = ReplyMode.Match,
                    IsEnabled = true,
                    SortOrder = 0,
                    MatchConfig = new MatchRuleConfig
                    {
                        TriggerPattern = "BB",
                        ResponseContent = "CC",
                        MatchType = MatchType.HexContains
                    }
                }
            ]
        });
        _service.Start();

        var frame1 = new byte[] { 0xBB }; // 匹配（第一帧）
        var frame2 = new byte[] { 0xAA }; // 不匹配（被忽略）

        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame1);
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame2);
        Thread.Sleep(100);

        // FIRST 模式只检查第一帧
        _sentReplies.Should().HaveCount(1);
    }

    [Fact]
    public void NoMatchRules_ShouldNotReply()
    {
        _service.UpdateConfig(new AutoReplyConfig
        {
            IsEnabled = true,
            DebounceWindowMs = 50,
            DecisionMode = DecisionMode.OR,
            Rules = [] // 无规则
        });
        _service.Start();

        var frame = new byte[] { 0xAA };
        _mockSerial.Raise(s => s.FrameReceived += null, _mockSerial.Object, frame);
        Thread.Sleep(100);

        _sentReplies.Should().BeEmpty();
    }
}
