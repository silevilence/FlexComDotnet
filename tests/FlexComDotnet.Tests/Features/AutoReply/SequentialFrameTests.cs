using FlexComDotnet.Core.Features.AutoReply.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.AutoReply;

public class SequentialFrameTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var frame = new SequentialFrame();

        // Assert
        frame.Id.Should().Be(0);
        frame.Name.Should().BeEmpty();
        frame.Content.Should().BeEmpty();
        frame.IsHexMode.Should().BeTrue();
        frame.IsEnabled.Should().BeTrue();
        frame.SortOrder.Should().Be(0);
        frame.Description.Should().BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange & Act
        var frame = new SequentialFrame
        {
            Id = 1,
            Name = "Frame 1",
            Content = "01 02 03",
            IsHexMode = false,
            IsEnabled = false,
            SortOrder = 3,
            Description = "Test Frame"
        };

        // Assert
        frame.Id.Should().Be(1);
        frame.Name.Should().Be("Frame 1");
        frame.Content.Should().Be("01 02 03");
        frame.IsHexMode.Should().BeFalse();
        frame.IsEnabled.Should().BeFalse();
        frame.SortOrder.Should().Be(3);
        frame.Description.Should().Be("Test Frame");
    }
}
