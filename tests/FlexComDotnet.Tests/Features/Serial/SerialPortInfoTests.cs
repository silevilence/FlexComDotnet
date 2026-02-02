using FlexComDotnet.Core.Features.Serial.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

public class SerialPortInfoTests
{
    [Fact]
    public void DisplayName_WithDescription_ShouldCombinePortAndDescription()
    {
        // Arrange
        var info = new SerialPortInfo
        {
            PortName = "COM3",
            Description = "USB-SERIAL CH340"
        };

        // Act & Assert
        info.DisplayName.Should().Be("COM3 - USB-SERIAL CH340");
    }

    [Fact]
    public void DisplayName_WithoutDescription_ShouldReturnPortNameOnly()
    {
        // Arrange
        var info = new SerialPortInfo
        {
            PortName = "COM1",
            Description = ""
        };

        // Act & Assert
        info.DisplayName.Should().Be("COM1");
    }

    [Fact]
    public void ToString_ShouldReturnDisplayName()
    {
        // Arrange
        var info = new SerialPortInfo
        {
            PortName = "COM5",
            Description = "Test Device"
        };

        // Act & Assert
        info.ToString().Should().Be("COM5 - Test Device");
    }
}
