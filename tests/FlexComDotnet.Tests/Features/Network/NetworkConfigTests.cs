using FlexComDotnet.Core.Features.Network.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

public class TcpClientConfigTests
{
    [Fact]
    public void DefaultConfig_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var config = new TcpClientConfig();

        // Assert
        config.ConnectionType.Should().Be(ConnectionType.TcpClient);
        config.Host.Should().Be("127.0.0.1");
        config.Port.Should().Be(8080);
        config.ConnectTimeout.Should().Be(5000);
        config.ReceiveTimeout.Should().Be(0);
        config.SendTimeout.Should().Be(0);
        config.KeepAlive.Should().BeTrue();
        config.NoDelay.Should().BeFalse();
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new TcpClientConfig
        {
            Host = "192.168.1.100",
            Port = 9999,
            ConnectTimeout = 10000,
            ReceiveTimeout = 5000,
            SendTimeout = 3000,
            KeepAlive = false,
            NoDelay = true
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.Host.Should().Be(original.Host);
        clone.Port.Should().Be(original.Port);
        clone.ConnectTimeout.Should().Be(original.ConnectTimeout);
        clone.ReceiveTimeout.Should().Be(original.ReceiveTimeout);
        clone.SendTimeout.Should().Be(original.SendTimeout);
        clone.KeepAlive.Should().Be(original.KeepAlive);
        clone.NoDelay.Should().Be(original.NoDelay);

        // Verify independence
        clone.Host = "10.0.0.1";
        original.Host.Should().Be("192.168.1.100");
    }
}

public class TcpServerConfigTests
{
    [Fact]
    public void DefaultConfig_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var config = new TcpServerConfig();

        // Assert
        config.ConnectionType.Should().Be(ConnectionType.TcpServer);
        config.ListenAddress.Should().Be("0.0.0.0");
        config.Port.Should().Be(8080);
        config.MaxConnections.Should().Be(10);
        config.Backlog.Should().Be(100);
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new TcpServerConfig
        {
            ListenAddress = "192.168.1.1",
            Port = 9999,
            MaxConnections = 50,
            Backlog = 200
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.ListenAddress.Should().Be(original.ListenAddress);
        clone.Port.Should().Be(original.Port);
        clone.MaxConnections.Should().Be(original.MaxConnections);
        clone.Backlog.Should().Be(original.Backlog);

        // Verify independence
        clone.Port = 1234;
        original.Port.Should().Be(9999);
    }
}

public class UdpConfigTests
{
    [Fact]
    public void DefaultConfig_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var config = new UdpConfig();

        // Assert
        config.ConnectionType.Should().Be(ConnectionType.Udp);
        config.LocalPort.Should().Be(0);
        config.RemoteHost.Should().Be("127.0.0.1");
        config.RemotePort.Should().Be(8080);
        config.EnableBroadcast.Should().BeFalse();
        config.ReceiveBufferSize.Should().Be(65536);
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new UdpConfig
        {
            LocalPort = 5000,
            RemoteHost = "192.168.1.255",
            RemotePort = 9999,
            EnableBroadcast = true,
            ReceiveBufferSize = 32768
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.LocalPort.Should().Be(original.LocalPort);
        clone.RemoteHost.Should().Be(original.RemoteHost);
        clone.RemotePort.Should().Be(original.RemotePort);
        clone.EnableBroadcast.Should().Be(original.EnableBroadcast);
        clone.ReceiveBufferSize.Should().Be(original.ReceiveBufferSize);

        // Verify independence
        clone.RemotePort = 1234;
        original.RemotePort.Should().Be(9999);
    }
}
