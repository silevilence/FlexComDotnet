using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Serial;

public class SerialPortServiceTests
{
    [Fact]
    public void NewService_ShouldNotBeConnected()
    {
        // Arrange & Act
        var service = new SerialPortService();

        // Assert
        service.IsConnected.Should().BeFalse();
        service.CurrentConfig.Should().BeNull();
    }

    [Fact]
    public void GetAvailablePorts_ShouldReturnList()
    {
        // Arrange
        var service = new SerialPortService();

        // Act
        var ports = service.GetAvailablePorts();

        // Assert
        ports.Should().NotBeNull();
        // 注意：实际端口列表取决于测试环境
    }

    [Fact]
    public void Open_WithInvalidPortName_ShouldReturnFalse()
    {
        // Arrange
        var service = new SerialPortService();
        var config = new SerialPortConfig
        {
            PortName = "INVALID_PORT_NAME",
            BaudRate = BaudRate.Baud115200
        };

        // Act
        var result = service.Open(config);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Open_WithEmptyPortName_ShouldReturnFalse()
    {
        // Arrange
        var service = new SerialPortService();
        var config = new SerialPortConfig
        {
            PortName = "",
            BaudRate = BaudRate.Baud115200
        };

        // Act
        var result = service.Open(config);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Close_WhenNotConnected_ShouldNotThrow()
    {
        // Arrange
        var service = new SerialPortService();

        // Act & Assert
        service.Invoking(s => s.Close()).Should().NotThrow();
    }

    [Fact]
    public void Send_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var service = new SerialPortService();

        // Act
        var result = service.Send(new byte[] { 0x01, 0x02, 0x03 });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SendString_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var service = new SerialPortService();

        // Act
        var result = service.Send("Hello");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ErrorOccurred_ShouldRaiseOnOpenFailure()
    {
        // Arrange
        var service = new SerialPortService();
        var config = new SerialPortConfig
        {
            PortName = "INVALID_PORT",
            BaudRate = BaudRate.Baud115200
        };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        service.Open(config);

        // Assert
        errorMessage.Should().NotBeNullOrEmpty();
    }
}
