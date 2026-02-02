using FlexComDotnet.Core.Features.Serial.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

public class SerialPortConfigTests
{
    [Fact]
    public void NewConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new SerialPortConfig();

        // Assert
        config.PortName.Should().BeEmpty();
        config.BaudRate.Should().Be(BaudRate.Baud115200);
        config.DataBits.Should().Be(DataBitsOption.Eight);
        config.StopBits.Should().Be(StopBitsOption.One);
        config.Parity.Should().Be(ParityOption.None);
        config.FlowControl.Should().Be(FlowControlOption.None);
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new SerialPortConfig
        {
            PortName = "COM1",
            BaudRate = BaudRate.Baud9600,
            DataBits = DataBitsOption.Seven,
            StopBits = StopBitsOption.Two,
            Parity = ParityOption.Even,
            FlowControl = FlowControlOption.RtsCts
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.PortName.Should().Be("COM1");
        clone.BaudRate.Should().Be(BaudRate.Baud9600);
        clone.DataBits.Should().Be(DataBitsOption.Seven);
        clone.StopBits.Should().Be(StopBitsOption.Two);
        clone.Parity.Should().Be(ParityOption.Even);
        clone.FlowControl.Should().Be(FlowControlOption.RtsCts);
    }

    [Fact]
    public void Clone_ModifyingClone_ShouldNotAffectOriginal()
    {
        // Arrange
        var original = new SerialPortConfig
        {
            PortName = "COM1",
            BaudRate = BaudRate.Baud9600
        };

        // Act
        var clone = original.Clone();
        clone.PortName = "COM2";
        clone.BaudRate = BaudRate.Baud115200;

        // Assert
        original.PortName.Should().Be("COM1");
        original.BaudRate.Should().Be(BaudRate.Baud9600);
    }
}
