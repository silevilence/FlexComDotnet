using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Settings.ViewModels;
using FlexComDotnet.Core.Features.Update.Models;
using FlexComDotnet.Core.Features.Update.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Settings;

/// <summary>
/// SettingsViewModel 测试
/// </summary>
public class SettingsViewModelTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IVersionService> _mockVersionService;
    private readonly Mock<IPanelManager> _mockPanelManager;
    private readonly string _testLogDir;

    public SettingsViewModelTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockVersionService = new Mock<IVersionService>();
        _mockPanelManager = new Mock<IPanelManager>();
        _testLogDir = Path.Combine(Path.GetTempPath(), "test-logs");

        // 默认配置
        _mockConfigService.Setup(s => s.Load()).Returns(new AppConfig());
        _mockVersionService.Setup(s => s.GetCurrentVersion())
            .Returns(new VersionInfo { Major = 1, Minor = 2, Patch = 3 });
        _mockPanelManager.Setup(s => s.Panels)
            .Returns(new List<PanelInfo>());
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _mockConfigService.Object,
            _mockVersionService.Object,
            _mockPanelManager.Object,
            _testLogDir);
    }

    [Fact]
    public void Constructor_ShouldLoadDebugModeFromConfig()
    {
        // Arrange
        var config = new AppConfig();
        config.DebugConfig.IsDebugModeEnabled = true;
        _mockConfigService.Setup(s => s.Load()).Returns(config);

        // Act
        var vm = CreateViewModel();

        // Assert
        vm.IsDebugModeEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldLoadCurrentVersion()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.CurrentVersion.Should().Be("v1.2.3");
    }

    [Fact]
    public void Constructor_WhenDebugModeDisabled_ShouldShowFalse()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.IsDebugModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDebugModeEnabled_WhenChanged_ShouldSaveToConfig()
    {
        // Arrange
        var vm = CreateViewModel();
        AppConfig? savedConfig = null;
        _mockConfigService.Setup(s => s.Save(It.IsAny<AppConfig>()))
            .Callback<AppConfig>(c => savedConfig = c);

        // Act
        vm.IsDebugModeEnabled = true;

        // Assert
        _mockConfigService.Verify(s => s.Save(It.IsAny<AppConfig>()), Times.Once);
        savedConfig.Should().NotBeNull();
        savedConfig!.DebugConfig.IsDebugModeEnabled.Should().BeTrue();
    }

    [Fact]
    public void LogDirectory_ShouldReturnProvidedPath()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.LogDirectory.Should().Be(_testLogDir);
    }

    [Fact]
    public void PanelItems_ShouldExcludeConnectionConfig()
    {
        // Arrange
        var panels = new List<PanelInfo>
        {
            new() { Id = "connection-config", Title = "连接配置", IsVisible = true },
            new() { Id = "command-list", Title = "指令列表", IsVisible = true },
            new() { Id = "auto-reply", Title = "自动回复", IsVisible = false }
        };
        _mockPanelManager.Setup(s => s.Panels).Returns(panels);

        // Act
        var vm = CreateViewModel();
        var items = vm.PanelItems.ToList();

        // Assert
        items.Should().HaveCount(2);
        items.Should().NotContain(p => p.Id == "connection-config");
    }

    [Fact]
    public void PanelItems_ShouldReflectVisibilityState()
    {
        // Arrange
        var panels = new List<PanelInfo>
        {
            new() { Id = "command-list", Title = "指令列表", IsVisible = true },
            new() { Id = "auto-reply", Title = "自动回复", IsVisible = false }
        };
        _mockPanelManager.Setup(s => s.Panels).Returns(panels);

        // Act
        var vm = CreateViewModel();
        var items = vm.PanelItems.ToList();

        // Assert
        items.First(p => p.Id == "command-list").IsVisible.Should().BeTrue();
        items.First(p => p.Id == "auto-reply").IsVisible.Should().BeFalse();
    }

    [Fact]
    public void TogglePanelVisibility_ShouldRaisePanelVisibilityToggledEvent()
    {
        // Arrange
        var vm = CreateViewModel();
        string? toggledPanelId = null;
        vm.PanelVisibilityToggled += (_, id) => toggledPanelId = id;

        // Act
        vm.TogglePanelVisibilityCommand.Execute("command-list");

        // Assert
        toggledPanelId.Should().Be("command-list");
    }
}
