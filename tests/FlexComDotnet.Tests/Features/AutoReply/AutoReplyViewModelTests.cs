using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.AutoReply.ViewModels;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class AutoReplyViewModelTests
{
    private readonly Mock<IAutoReplyService> _mockAutoReplyService;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly AutoReplyViewModel _viewModel;

    public AutoReplyViewModelTests()
    {
        _mockAutoReplyService = new Mock<IAutoReplyService>();
        _mockConfigService = new Mock<IConfigurationService>();

        // Setup default config
        _mockAutoReplyService.Setup(s => s.Config).Returns(new AutoReplyConfig());
        _mockConfigService.Setup(s => s.Load()).Returns(new AppConfig());

        _viewModel = new AutoReplyViewModel(_mockAutoReplyService.Object, _mockConfigService.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        _viewModel.GlobalDelayMs.Should().Be(100);
        _viewModel.IsRunning.Should().BeFalse();
        _viewModel.ReceiveCount.Should().Be(0);
        _viewModel.ReplyCount.Should().Be(0);
        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public void ToggleRunning_WhenNotRunning_ShouldStartService()
    {
        // Act
        _viewModel.ToggleRunningCommand.Execute(null);

        // Assert
        _mockAutoReplyService.Verify(s => s.Start(), Times.Once);
        _viewModel.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void ToggleRunning_WhenRunning_ShouldStopService()
    {
        // Arrange - Start first
        _viewModel.ToggleRunningCommand.Execute(null);
        _mockAutoReplyService.Invocations.Clear();

        // Act - Toggle again to stop
        _viewModel.ToggleRunningCommand.Execute(null);

        // Assert
        _mockAutoReplyService.Verify(s => s.Stop(), Times.Once);
        _viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ResetCounters_ShouldResetBothCounters()
    {
        // Act
        _viewModel.ResetCountersCommand.Execute(null);

        // Assert
        _mockAutoReplyService.Verify(s => s.ResetCounters(), Times.Once);
        _viewModel.ReceiveCount.Should().Be(0);
        _viewModel.ReplyCount.Should().Be(0);
    }

    [Fact]
    public void AddMatchRule_ShouldAddNewRule()
    {
        // Act
        _viewModel.AddMatchRuleCommand.Execute(null);

        // Assert
        _viewModel.Rules.Should().HaveCount(1);
        _viewModel.SelectedRule.Should().NotBeNull();
        _viewModel.SelectedRule!.Type.Should().Be(ReplyMode.Match);
    }

    [Fact]
    public void AddSequentialRule_ShouldAddNewRule()
    {
        // Act
        _viewModel.AddSequentialRuleCommand.Execute(null);

        // Assert
        _viewModel.Rules.Should().HaveCount(1);
        _viewModel.SelectedRule.Should().NotBeNull();
        _viewModel.SelectedRule!.Type.Should().Be(ReplyMode.Sequential);
    }

    [Fact]
    public void AddProtocolRule_ShouldAddNewRule()
    {
        // Act
        _viewModel.AddProtocolRuleCommand.Execute(null);

        // Assert
        _viewModel.Rules.Should().HaveCount(1);
        _viewModel.SelectedRule.Should().NotBeNull();
        _viewModel.SelectedRule!.Type.Should().Be(ReplyMode.Protocol);
    }

    [Fact]
    public void RemoveRule_WhenSelected_ShouldRemoveRule()
    {
        // Arrange
        _viewModel.AddMatchRuleCommand.Execute(null);

        // Act
        _viewModel.RemoveRuleCommand.Execute(null);

        // Assert
        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_WhenNotSelected_ShouldNotExecute()
    {
        // Arrange
        _viewModel.SelectedRule = null;

        // Assert
        _viewModel.RemoveRuleCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void MoveRuleUp_ShouldMoveRule()
    {
        // Arrange
        _viewModel.AddMatchRuleCommand.Execute(null);
        _viewModel.AddSequentialRuleCommand.Execute(null);
        var secondRule = _viewModel.SelectedRule;

        // Act
        _viewModel.MoveRuleUpCommand.Execute(null);

        // Assert
        _viewModel.Rules[0].Should().Be(secondRule);
    }

    [Fact]
    public void MoveRuleDown_ShouldMoveRule()
    {
        // Arrange
        _viewModel.AddMatchRuleCommand.Execute(null);
        var firstRule = _viewModel.SelectedRule;
        _viewModel.AddSequentialRuleCommand.Execute(null);
        _viewModel.SelectedRule = firstRule;

        // Act
        _viewModel.MoveRuleDownCommand.Execute(null);

        // Assert
        _viewModel.Rules[1].Should().Be(firstRule);
    }

    [Fact]
    public void AddFrame_ShouldAddNewFrameToEditingFrames()
    {
        // Arrange - 先添加一个顺序规则并选中
        _viewModel.AddSequentialRuleCommand.Execute(null);

        // Act
        _viewModel.AddFrameCommand.Execute(null);

        // Assert
        _viewModel.EditingFrames.Should().HaveCount(1);
        _viewModel.SelectedEditingFrame.Should().NotBeNull();
        _viewModel.SelectedEditingFrame!.Name.Should().Be("帧 1");
    }

    [Fact]
    public void RemoveFrame_WhenSelected_ShouldRemoveFrame()
    {
        // Arrange
        _viewModel.AddSequentialRuleCommand.Execute(null);
        _viewModel.AddFrameCommand.Execute(null);

        // Act
        _viewModel.RemoveFrameCommand.Execute(null);

        // Assert
        _viewModel.EditingFrames.Should().BeEmpty();
    }

    [Fact]
    public void ClearLogs_ShouldClearAllLogs()
    {
        // Arrange - 添加一些日志
        _viewModel.ReplyLogs.Add(new ReplyLogEntry { RuleName = "Test" });

        // Act
        _viewModel.ClearLogsCommand.Execute(null);

        // Assert
        _viewModel.ReplyLogs.Should().BeEmpty();
    }

    [Fact]
    public void ToggleRunning_ShouldAutoSave()
    {
        // Act
        _viewModel.ToggleRunningCommand.Execute(null);

        // Assert
        _mockConfigService.Verify(s => s.Save(It.IsAny<AppConfig>()), Times.Once);
    }

    [Fact]
    public void LoadConfig_ShouldLoadFromService()
    {
        // Arrange
        var config = new AppConfig
        {
            AutoReplyConfig = new AutoReplyConfig
            {
                IsEnabled = true,
                GlobalDelayMs = 250,
                Rules =
                [
                    new AutoReplyRule
                    {
                        Name = "Rule1",
                        Type = ReplyMode.Match,
                        SortOrder = 0,
                        IsEnabled = true,
                        MatchConfig = new MatchRuleConfig()
                    },
                    new AutoReplyRule
                    {
                        Name = "SeqRule1",
                        Type = ReplyMode.Sequential,
                        SortOrder = 1,
                        IsEnabled = true,
                        SequentialConfig = new SequentialRuleConfig
                        {
                            Frames =
                            [
                                new SequentialFrame { Name = "Frame1", SortOrder = 0 }
                            ],
                            EnableLoop = false,
                            CurrentIndex = 2
                        }
                    }
                ]
            }
        };

        _mockConfigService.Setup(s => s.Load()).Returns(config);

        // Act
        var newViewModel = new AutoReplyViewModel(_mockAutoReplyService.Object, _mockConfigService.Object);

        // Assert
        newViewModel.IsRunning.Should().BeFalse();  // Always starts stopped
        newViewModel.GlobalDelayMs.Should().Be(250);
        newViewModel.Rules.Should().HaveCount(2);
        newViewModel.Rules[0].Name.Should().Be("Rule1");
        newViewModel.Rules[0].Type.Should().Be(ReplyMode.Match);
        newViewModel.Rules[1].Name.Should().Be("SeqRule1");
        newViewModel.Rules[1].Type.Should().Be(ReplyMode.Sequential);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromEvents()
    {
        // Act & Assert - should not throw
        var action = () => _viewModel.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void LoadConfig_ShouldNotTriggerAutoSave()
    {
        // Arrange - 配置中包含规则数据
        var config = new AppConfig
        {
            AutoReplyConfig = new AutoReplyConfig
            {
                GlobalDelayMs = 200,
                Rules =
                [
                    new AutoReplyRule
                    {
                        Name = "TestRule",
                        Type = ReplyMode.Match,
                        SortOrder = 0,
                        IsEnabled = true,
                        MatchConfig = new MatchRuleConfig { TriggerPattern = "AA BB" }
                    },
                    new AutoReplyRule
                    {
                        Name = "TestSeqRule",
                        Type = ReplyMode.Sequential,
                        SortOrder = 1,
                        IsEnabled = true,
                        SequentialConfig = new SequentialRuleConfig
                        {
                            Frames =
                            [
                                new SequentialFrame { Name = "TestFrame", SortOrder = 0, Content = "CC DD" }
                            ],
                            EnableLoop = false
                        }
                    }
                ]
            }
        };

        var mockConfig = new Mock<IConfigurationService>();
        mockConfig.Setup(s => s.Load()).Returns(config);

        var mockAutoReply = new Mock<IAutoReplyService>();
        mockAutoReply.Setup(s => s.Config).Returns(new AutoReplyConfig());

        // Act - 创建 ViewModel（触发 LoadConfig）
        var vm = new AutoReplyViewModel(mockAutoReply.Object, mockConfig.Object);

        // Assert - 加载期间不应触发 Save（即 AutoSave 不应执行）
        mockConfig.Verify(s => s.Save(It.IsAny<AppConfig>()), Times.Never,
            "LoadConfig 期间不应触发 AutoSave，否则会用空集合覆盖已保存的规则");

        // 规则应正确加载
        vm.Rules.Should().HaveCount(2);
        vm.Rules[0].Name.Should().Be("TestRule");
        vm.Rules[0].Type.Should().Be(ReplyMode.Match);
        vm.Rules[1].Name.Should().Be("TestSeqRule");
        vm.Rules[1].Type.Should().Be(ReplyMode.Sequential);
    }
}
