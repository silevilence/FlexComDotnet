using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Visualization.Models;
using FlexComDotnet.Core.Features.Visualization.Services;
using FlexComDotnet.Core.Features.Visualization.ViewModels;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Visualization;

public class DataVisualizationViewModelTests
{
    private readonly Mock<IVisualizationService> _mockVisualizationService;
    private readonly Mock<IProtocolParserService> _mockParserService;
    private readonly DataVisualizationViewModel _viewModel;

    public DataVisualizationViewModelTests()
    {
        _mockVisualizationService = new Mock<IVisualizationService>();
        _mockParserService = new Mock<IProtocolParserService>();
        _mockParserService.Setup(p => p.GetAllParsers()).Returns([]);

        _viewModel = new DataVisualizationViewModel(
            _mockVisualizationService.Object,
            _mockParserService.Object);
    }

    #region 构造函数测试

    [Fact]
    public void Constructor_WithNullVisualizationService_ShouldThrow()
    {
        var act = () => new DataVisualizationViewModel(null!, _mockParserService.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullParserService_ShouldThrow()
    {
        var act = () => new DataVisualizationViewModel(_mockVisualizationService.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldInitializeDefaultValues()
    {
        _viewModel.IsRunning.Should().BeFalse();
        _viewModel.IsPaused.Should().BeFalse();
        _viewModel.MaxDataPoints.Should().Be(1000);
        _viewModel.StatusMessage.Should().Be("就绪");
        _viewModel.TotalDataPoints.Should().Be(0);
    }

    #endregion

    #region 解析器列表测试

    [Fact]
    public void RefreshParsers_ShouldLoadAvailableParsers()
    {
        // Arrange
        var mockParser1 = new Mock<IProtocolParser>();
        mockParser1.Setup(p => p.Name).Returns("Protocol1");
        var mockParser2 = new Mock<IProtocolParser>();
        mockParser2.Setup(p => p.Name).Returns("Protocol2");

        _mockParserService.Setup(p => p.GetAllParsers())
            .Returns(new List<IProtocolParser> { mockParser1.Object, mockParser2.Object });

        // Act
        _viewModel.RefreshParsersCommand.Execute(null);

        // Assert
        _viewModel.AvailableParsers.Should().HaveCount(2);
        _viewModel.AvailableParsers.Should().Contain("Protocol1");
        _viewModel.AvailableParsers.Should().Contain("Protocol2");
    }

    [Fact]
    public void SelectingParser_ShouldRefreshAvailableFields()
    {
        // Arrange
        var definition = new FrameDefinition
        {
            Name = "TestProtocol",
            Fields =
            [
                new FieldDefinition { Name = "温度", IsEnabled = true },
                new FieldDefinition { Name = "湿度", IsEnabled = true },
                new FieldDefinition { Name = "Disabled", IsEnabled = false }
            ]
        };

        var mockParser = new Mock<IProtocolParser>();
        mockParser.Setup(p => p.Name).Returns("TestProtocol");
        mockParser.Setup(p => p.Definition).Returns(definition);

        _mockParserService.Setup(p => p.GetParser("TestProtocol")).Returns(mockParser.Object);

        // Act
        _viewModel.SelectedParserName = "TestProtocol";

        // Assert
        _viewModel.AvailableFields.Should().HaveCount(2);
        _viewModel.AvailableFields.Should().Contain("温度");
        _viewModel.AvailableFields.Should().Contain("湿度");
        _viewModel.AvailableFields.Should().NotContain("Disabled");
    }

    #endregion

    #region 通道管理测试

    [Fact]
    public void AddChannel_ShouldAddToChannelsCollection()
    {
        // Arrange
        _mockVisualizationService.Setup(s => s.GetChannels()).Returns(new List<ChannelConfig>());
        _viewModel.SelectedFieldName = "温度";

        // Act
        _viewModel.AddChannelCommand.Execute(null);

        // Assert
        _viewModel.Channels.Should().HaveCount(1);
        _viewModel.Channels[0].FieldName.Should().Be("温度");
        _viewModel.Channels[0].DisplayName.Should().Be("温度");
        _mockVisualizationService.Verify(s => s.AddChannel(It.IsAny<ChannelConfig>()), Times.Once);
    }

    [Fact]
    public void AddChannel_WithDuplicateField_ShouldNotAdd()
    {
        // Arrange
        _mockVisualizationService.Setup(s => s.GetChannels())
            .Returns(new List<ChannelConfig>
            {
                new() { Id = "ch1", FieldName = "温度" }
            });

        _viewModel.SelectedFieldName = "温度";

        // Act
        _viewModel.AddChannelCommand.Execute(null);

        // Assert
        _viewModel.StatusMessage.Should().Contain("已添加");
    }

    [Fact]
    public void AddChannel_WithNoFieldSelected_ShouldNotExecute()
    {
        // Arrange
        _viewModel.SelectedFieldName = null;

        // Assert
        _viewModel.AddChannelCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void RemoveChannel_ShouldRemoveFromCollection()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "温度", DisplayName = "温度" };
        _viewModel.Channels.Add(channel);
        _viewModel.SelectedChannel = channel;

        // Act
        _viewModel.RemoveChannelCommand.Execute(null);

        // Assert
        _viewModel.Channels.Should().BeEmpty();
        _mockVisualizationService.Verify(s => s.RemoveChannel("ch1"), Times.Once);
    }

    [Fact]
    public void RemoveChannel_WithNoSelection_ShouldNotExecute()
    {
        _viewModel.SelectedChannel = null;
        _viewModel.RemoveChannelCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ToggleChannelVisibility_ShouldToggleVisibility()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "温度", IsVisible = true };
        _viewModel.SelectedChannel = channel;

        // Act
        _viewModel.ToggleChannelVisibilityCommand.Execute(null);

        // Assert
        channel.IsVisible.Should().BeFalse();
        _mockVisualizationService.Verify(s => s.UpdateChannel(channel), Times.Once);
    }

    [Fact]
    public void AddChannel_ShouldCycleThroughColors()
    {
        // Arrange
        _mockVisualizationService.Setup(s => s.GetChannels()).Returns(new List<ChannelConfig>());

        _viewModel.SelectedFieldName = "Field1";
        _viewModel.AddChannelCommand.Execute(null);

        _mockVisualizationService.Setup(s => s.GetChannels())
            .Returns(new List<ChannelConfig> { new() { FieldName = "Field1" } });

        _viewModel.SelectedFieldName = "Field2";
        _viewModel.AddChannelCommand.Execute(null);

        // Assert - should have different colors
        _viewModel.Channels.Should().HaveCount(2);
        _viewModel.Channels[0].Color.Should().NotBe(_viewModel.Channels[1].Color);
    }

    #endregion

    #region 采集控制测试

    [Fact]
    public void Start_WithParserSelected_ShouldStartService()
    {
        // Arrange
        _viewModel.SelectedParserName = "TestProtocol";

        // Act
        _viewModel.StartCommand.Execute(null);

        // Assert
        _mockVisualizationService.VerifySet(s => s.SelectedParserName = "TestProtocol", Times.Once);
        _mockVisualizationService.Verify(s => s.Start(), Times.Once);
    }

    [Fact]
    public void Start_WithoutParserSelected_ShouldNotExecute()
    {
        _viewModel.SelectedParserName = null;
        _viewModel.StartCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Stop_ShouldStopService()
    {
        // Arrange
        _viewModel.IsRunning = true;

        // Act
        _viewModel.StopCommand.Execute(null);

        // Assert
        _mockVisualizationService.Verify(s => s.Stop(), Times.Once);
    }

    [Fact]
    public void Stop_WhenNotRunning_ShouldNotExecute()
    {
        _viewModel.IsRunning = false;
        _viewModel.StopCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void TogglePause_ShouldToggleState()
    {
        // Act
        _viewModel.TogglePauseCommand.Execute(null);

        // Assert
        _viewModel.IsPaused.Should().BeTrue();
        _viewModel.StatusMessage.Should().Contain("暂停");

        // Act again
        _viewModel.TogglePauseCommand.Execute(null);

        // Assert
        _viewModel.IsPaused.Should().BeFalse();
    }

    #endregion

    #region 数据操作测试

    [Fact]
    public void ClearData_ShouldCallServiceAndResetCount()
    {
        // Arrange
        _viewModel.TotalDataPoints = 100;

        // Act
        _viewModel.ClearDataCommand.Execute(null);

        // Assert
        _mockVisualizationService.Verify(s => s.ClearData(), Times.Once);
        _viewModel.TotalDataPoints.Should().Be(0);
    }

    [Fact]
    public void ExportCsv_ShouldCallService()
    {
        // Arrange
        var filePath = "test.csv";

        // Act
        _viewModel.ExportCsvCommand.Execute(filePath);

        // Assert
        _mockVisualizationService.Verify(s => s.ExportToCsv(filePath), Times.Once);
    }

    [Fact]
    public void ExportCsv_WithNullPath_ShouldNotCallService()
    {
        // Act
        _viewModel.ExportCsvCommand.Execute(null);

        // Assert
        _mockVisualizationService.Verify(s => s.ExportToCsv(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ExportPng_ShouldFireEvent()
    {
        // Arrange
        string? receivedPath = null;
        _viewModel.ExportPngRequested += (sender, path) => receivedPath = path;

        // Act
        _viewModel.ExportPngCommand.Execute("test.png");

        // Assert
        receivedPath.Should().Be("test.png");
    }

    [Fact]
    public void PushParsedFrame_ShouldDelegateToService()
    {
        // Arrange
        var frame = new ParsedFrame { IsValid = true };

        // Act
        _viewModel.PushParsedFrame(frame);

        // Assert
        _mockVisualizationService.Verify(s => s.PushData(frame), Times.Once);
    }

    #endregion

    #region 事件响应测试

    [Fact]
    public void OnStateChanged_ShouldUpdateIsRunning()
    {
        // Act
        _mockVisualizationService.Raise(s => s.StateChanged += null,
            new VisualizationStateChangedEventArgs(true));

        // Assert
        _viewModel.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void OnDataPointAdded_ShouldIncrementTotalDataPoints()
    {
        // Arrange
        var dataPoint = new ChartDataPoint("ch1", 25.5, DateTime.Now);

        // Act
        _mockVisualizationService.Raise(s => s.DataPointAdded += null,
            new DataPointAddedEventArgs(dataPoint));

        // Assert
        _viewModel.TotalDataPoints.Should().Be(1);
    }

    [Fact]
    public void OnDataPointAdded_WhenNotPaused_ShouldRequestChartRefresh()
    {
        // Arrange
        bool refreshRequested = false;
        _viewModel.ChartRefreshRequested += (sender, e) => refreshRequested = true;
        var dataPoint = new ChartDataPoint("ch1", 25.5, DateTime.Now);

        // Act
        _mockVisualizationService.Raise(s => s.DataPointAdded += null,
            new DataPointAddedEventArgs(dataPoint));

        // Assert
        refreshRequested.Should().BeTrue();
    }

    [Fact]
    public void OnDataPointAdded_WhenPaused_ShouldNotRequestChartRefresh()
    {
        // Arrange
        bool refreshRequested = false;
        _viewModel.ChartRefreshRequested += (sender, e) => refreshRequested = true;
        _viewModel.IsPaused = true;
        var dataPoint = new ChartDataPoint("ch1", 25.5, DateTime.Now);

        // Act
        _mockVisualizationService.Raise(s => s.DataPointAdded += null,
            new DataPointAddedEventArgs(dataPoint));

        // Assert
        refreshRequested.Should().BeFalse();
    }

    [Fact]
    public void OnDataCleared_ShouldResetTotalDataPoints()
    {
        // Arrange
        _viewModel.TotalDataPoints = 100;

        // Act
        _mockVisualizationService.Raise(s => s.DataCleared += null, EventArgs.Empty);

        // Assert
        _viewModel.TotalDataPoints.Should().Be(0);
    }

    #endregion

    #region 自动添加通道测试

    [Fact]
    public void Start_WhenNoChannelsExist_ShouldAutoAddChannelsForAllFields()
    {
        // Arrange
        var definition = new FrameDefinition
        {
            Name = "TestProtocol",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };
        definition.Fields.Add(new FieldDefinition { Name = "Field1", IsEnabled = true });
        definition.Fields.Add(new FieldDefinition { Name = "Field2", IsEnabled = true });
        definition.Fields.Add(new FieldDefinition { Name = "Disabled", IsEnabled = false });

        var mockParser = new Mock<IProtocolParser>();
        mockParser.Setup(p => p.Name).Returns("TestProtocol");
        mockParser.Setup(p => p.Definition).Returns(definition);

        _mockParserService.Setup(p => p.GetParser("TestProtocol")).Returns(mockParser.Object);
        _mockVisualizationService.Setup(s => s.GetChannels()).Returns([]);

        _viewModel.SelectedParserName = "TestProtocol";

        // Act
        _viewModel.StartCommand.Execute(null);

        // Assert - 应自动添加 2 个启用的字段为通道
        _mockVisualizationService.Verify(
            s => s.AddChannel(It.Is<ChannelConfig>(c => c.FieldName == "Field1")),
            Times.Once);
        _mockVisualizationService.Verify(
            s => s.AddChannel(It.Is<ChannelConfig>(c => c.FieldName == "Field2")),
            Times.Once);
        _mockVisualizationService.Verify(
            s => s.AddChannel(It.Is<ChannelConfig>(c => c.FieldName == "Disabled")),
            Times.Never);
        _viewModel.Channels.Should().HaveCount(2);
    }

    [Fact]
    public void Start_WhenChannelsAlreadyExist_ShouldNotAutoAddChannels()
    {
        // Arrange
        var existingChannel = new ChannelConfig
        {
            Id = "existing",
            FieldName = "Field1",
            IsVisible = true
        };

        _mockVisualizationService.Setup(s => s.GetChannels())
            .Returns(new List<ChannelConfig> { existingChannel });

        _viewModel.SelectedParserName = "TestProtocol";
        _viewModel.Channels.Add(existingChannel);

        // Act
        _viewModel.StartCommand.Execute(null);

        // Assert - 不应自动添加通道
        _mockVisualizationService.Verify(
            s => s.AddChannel(It.IsAny<ChannelConfig>()),
            Times.Never);
    }

    #endregion
}
