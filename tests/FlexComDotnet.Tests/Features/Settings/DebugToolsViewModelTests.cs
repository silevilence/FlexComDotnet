using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;
using FlexComDotnet.Core.Features.Settings.ViewModels;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Settings;

/// <summary>
/// DebugToolsViewModel 测试
/// </summary>
public class DebugToolsViewModelTests
{
    private readonly Mock<ILoggingService> _mockLoggingService;

    public DebugToolsViewModelTests()
    {
        _mockLoggingService = new Mock<ILoggingService>();
    }

    private DebugToolsViewModel CreateViewModel()
    {
        return new DebugToolsViewModel(_mockLoggingService.Object);
    }

    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.SelectedSource.Should().Be(LogSource.System);
        vm.SelectedLevel.Should().Be(LogLevel.Info);
        vm.LogContent.Should().Be("test log");
    }

    [Fact]
    public void AvailableSources_ShouldContainAllLogSources()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.AvailableSources.Should().Contain(LogSource.System);
        vm.AvailableSources.Should().Contain(LogSource.Serial);
        vm.AvailableSources.Should().Contain(LogSource.Network);
        vm.AvailableSources.Should().Contain(LogSource.Script);
        vm.AvailableSources.Should().Contain(LogSource.AutoReply);
        vm.AvailableSources.Should().Contain(LogSource.Protocol);
    }

    [Fact]
    public void AvailableLevels_ShouldContainAllLogLevels()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.AvailableLevels.Should().Contain(LogLevel.Debug);
        vm.AvailableLevels.Should().Contain(LogLevel.Info);
        vm.AvailableLevels.Should().Contain(LogLevel.Warning);
        vm.AvailableLevels.Should().Contain(LogLevel.Error);
    }

    [Fact]
    public void SendLog_ShouldCallLoggingServiceWithSelectedValues()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedSource = LogSource.Serial;
        vm.SelectedLevel = LogLevel.Warning;
        vm.LogContent = "custom test message";

        // Act
        vm.SendLogCommand.Execute(null);

        // Assert
        _mockLoggingService.Verify(
            s => s.Log(LogLevel.Warning, LogSource.Serial, "custom test message"),
            Times.Once);
    }

    [Fact]
    public void SendLog_WhenContentIsEmpty_ShouldUseDefaultMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.LogContent = "";

        // Act
        vm.SendLogCommand.Execute(null);

        // Assert
        _mockLoggingService.Verify(
            s => s.Log(LogLevel.Info, LogSource.System, "test log"),
            Times.Once);
    }

    [Fact]
    public void SendLog_WhenContentIsWhitespace_ShouldUseDefaultMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.LogContent = "   ";

        // Act
        vm.SendLogCommand.Execute(null);

        // Assert
        _mockLoggingService.Verify(
            s => s.Log(LogLevel.Info, LogSource.System, "test log"),
            Times.Once);
    }

    [Fact]
    public void SelectedSource_ShouldBeChangeable()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SelectedSource = LogSource.Network;

        // Assert
        vm.SelectedSource.Should().Be(LogSource.Network);
    }

    [Fact]
    public void SelectedLevel_ShouldBeChangeable()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SelectedLevel = LogLevel.Error;

        // Assert
        vm.SelectedLevel.Should().Be(LogLevel.Error);
    }
}
