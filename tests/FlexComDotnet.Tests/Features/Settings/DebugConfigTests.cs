using FlexComDotnet.Core.Features.Settings.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Settings;

/// <summary>
/// DebugConfig 模型测试
/// </summary>
public class DebugConfigTests
{
    [Fact]
    public void NewDebugConfig_ShouldHaveDebugModeDisabled()
    {
        // Act
        var config = new DebugConfig();

        // Assert
        config.IsDebugModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDebugModeEnabled_ShouldBeSettable()
    {
        // Arrange
        var config = new DebugConfig();

        // Act
        config.IsDebugModeEnabled = true;

        // Assert
        config.IsDebugModeEnabled.Should().BeTrue();
    }
}
