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
        _viewModel.ActiveMode.Should().Be(ReplyMode.Match);
        _viewModel.IsRunning.Should().BeFalse();
        _viewModel.ReceiveCount.Should().Be(0);
        _viewModel.ReplyCount.Should().Be(0);
        _viewModel.MatchRules.Should().BeEmpty();
        _viewModel.SequentialFrames.Should().BeEmpty();
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
    public void ResetSequenceIndex_ShouldResetIndex()
    {
        // Act
        _viewModel.ResetSequenceIndexCommand.Execute(null);

        // Assert
        _mockAutoReplyService.Verify(s => s.ResetHandlerState(), Times.Once);
        _viewModel.CurrentFrameIndex.Should().Be(0);
    }

    [Fact]
    public void AddMatchRule_ShouldAddNewRule()
    {
        // Act
        _viewModel.AddMatchRuleCommand.Execute(null);

        // Assert
        _viewModel.MatchRules.Should().HaveCount(1);
        _viewModel.SelectedMatchRule.Should().NotBeNull();
        _viewModel.SelectedMatchRule!.Name.Should().Be("规则 1");
    }

    [Fact]
    public void RemoveMatchRule_WhenSelected_ShouldRemoveRule()
    {
        // Arrange
        _viewModel.AddMatchRuleCommand.Execute(null);
        var ruleToRemove = _viewModel.SelectedMatchRule;

        // Act
        _viewModel.RemoveMatchRuleCommand.Execute(null);

        // Assert
        _viewModel.MatchRules.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMatchRule_WhenNotSelected_ShouldNotExecute()
    {
        // Arrange
        _viewModel.SelectedMatchRule = null;

        // Assert
        _viewModel.RemoveMatchRuleCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void MoveMatchRuleUp_ShouldMoveRule()
    {
        // Arrange
        _viewModel.AddMatchRuleCommand.Execute(null);
        _viewModel.AddMatchRuleCommand.Execute(null);
        var secondRule = _viewModel.SelectedMatchRule;

        // Act
        _viewModel.MoveMatchRuleUpCommand.Execute(null);

        // Assert
        _viewModel.MatchRules[0].Should().Be(secondRule);
    }

    [Fact]
    public void MoveMatchRuleDown_ShouldMoveRule()
    {
        // Arrange
        _viewModel.AddMatchRuleCommand.Execute(null);
        var firstRule = _viewModel.SelectedMatchRule;
        _viewModel.AddMatchRuleCommand.Execute(null);
        _viewModel.SelectedMatchRule = firstRule;

        // Act
        _viewModel.MoveMatchRuleDownCommand.Execute(null);

        // Assert
        _viewModel.MatchRules[1].Should().Be(firstRule);
    }

    [Fact]
    public void AddSequentialFrame_ShouldAddNewFrame()
    {
        // Act
        _viewModel.AddSequentialFrameCommand.Execute(null);

        // Assert
        _viewModel.SequentialFrames.Should().HaveCount(1);
        _viewModel.SelectedSequentialFrame.Should().NotBeNull();
        _viewModel.SelectedSequentialFrame!.Name.Should().Be("帧 1");
    }

    [Fact]
    public void RemoveSequentialFrame_WhenSelected_ShouldRemoveFrame()
    {
        // Arrange
        _viewModel.AddSequentialFrameCommand.Execute(null);

        // Act
        _viewModel.RemoveSequentialFrameCommand.Execute(null);

        // Assert
        _viewModel.SequentialFrames.Should().BeEmpty();
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
                IsEnabled = true,  // This is saved but IsRunning starts as false
                GlobalDelayMs = 250,
                ActiveMode = ReplyMode.Sequential,
                MatchConfig = new MatchReplyConfig
                {
                    Rules =
                    [
                        new MatchRule { Name = "Rule1", SortOrder = 0 }
                    ]
                },
                SequentialConfig = new SequentialReplyConfig
                {
                    Frames =
                    [
                        new SequentialFrame { Name = "Frame1", SortOrder = 0 }
                    ],
                    EnableLoop = false,
                    CurrentIndex = 2
                }
            }
        };

        _mockConfigService.Setup(s => s.Load()).Returns(config);

        // Act
        var newViewModel = new AutoReplyViewModel(_mockAutoReplyService.Object, _mockConfigService.Object);

        // Assert
        newViewModel.IsRunning.Should().BeFalse();  // Always starts stopped
        newViewModel.GlobalDelayMs.Should().Be(250);
        newViewModel.ActiveMode.Should().Be(ReplyMode.Sequential);
        newViewModel.MatchRules.Should().HaveCount(1);
        newViewModel.MatchRules[0].Name.Should().Be("Rule1");
        newViewModel.SequentialFrames.Should().HaveCount(1);
        newViewModel.SequentialFrames[0].Name.Should().Be("Frame1");
        newViewModel.EnableLoop.Should().BeFalse();
        newViewModel.CurrentFrameIndex.Should().Be(2);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromEvents()
    {
        // Act & Assert - should not throw
        var action = () => _viewModel.Dispose();
        action.Should().NotThrow();
    }
}
