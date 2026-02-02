using FlexComDotnet.Core.Features.Serial.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// CommandItem 模型测试
/// </summary>
public class CommandItemTests
{
    [Fact]
    public void CommandItem_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var item = new CommandItem();

        // Assert
        item.Id.Should().Be(0);
        item.Name.Should().BeEmpty();
        item.Content.Should().BeEmpty();
        item.Description.Should().BeEmpty();
        item.IsHexMode.Should().BeFalse();
        item.IsEnabled.Should().BeTrue();
        item.SortOrder.Should().Be(0);
        item.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        item.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CommandItem_CanSetProperties()
    {
        // Arrange
        var now = DateTime.Now;

        // Act
        var item = new CommandItem
        {
            Id = 1,
            Name = "Test Command",
            Content = "AA BB CC",
            Description = "A test command",
            IsHexMode = true,
            IsEnabled = false,
            SortOrder = 5,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        item.Id.Should().Be(1);
        item.Name.Should().Be("Test Command");
        item.Content.Should().Be("AA BB CC");
        item.Description.Should().Be("A test command");
        item.IsHexMode.Should().BeTrue();
        item.IsEnabled.Should().BeFalse();
        item.SortOrder.Should().Be(5);
        item.CreatedAt.Should().Be(now);
        item.UpdatedAt.Should().Be(now);
    }
}
