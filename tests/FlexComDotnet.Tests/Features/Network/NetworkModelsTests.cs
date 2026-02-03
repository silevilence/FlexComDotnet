using System.Net;
using FlexComDotnet.Core.Features.Network.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

public class DataReceivedEventArgsTests
{
    [Fact]
    public void Constructor_WithDataOnly_ShouldSetProperties()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var args = new DataReceivedEventArgs(data);

        // Assert
        args.Data.Should().BeEquivalentTo(data);
        args.RemoteEndPoint.Should().BeNull();
        args.ReceivedTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithDataAndEndPoint_ShouldSetProperties()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var endPoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 8080);

        // Act
        var args = new DataReceivedEventArgs(data, endPoint);

        // Assert
        args.Data.Should().BeEquivalentTo(data);
        args.RemoteEndPoint.Should().Be(endPoint);
        args.ReceivedTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }
}

public class ClientInfoTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var id = "client-123";
        var endPoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 12345);

        // Act
        var clientInfo = new ClientInfo(id, endPoint);

        // Assert
        clientInfo.Id.Should().Be(id);
        clientInfo.RemoteEndPoint.Should().Be(endPoint);
        clientInfo.ConnectedTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        clientInfo.LastActivityTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        clientInfo.ReceivedBytes.Should().Be(0);
        clientInfo.SentBytes.Should().Be(0);
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var id = "client-123";
        var endPoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 12345);
        var clientInfo = new ClientInfo(id, endPoint);

        // Act
        var result = clientInfo.ToString();

        // Assert
        result.Should().Be("client-123: 192.168.1.100:12345");
    }

    [Fact]
    public void Properties_ShouldBeUpdatable()
    {
        // Arrange
        var clientInfo = new ClientInfo("client-1", new IPEndPoint(IPAddress.Loopback, 8080));

        // Act
        clientInfo.ReceivedBytes = 1024;
        clientInfo.SentBytes = 512;
        clientInfo.LastActivityTime = DateTime.Now.AddMinutes(5);

        // Assert
        clientInfo.ReceivedBytes.Should().Be(1024);
        clientInfo.SentBytes.Should().Be(512);
        clientInfo.LastActivityTime.Should().BeCloseTo(DateTime.Now.AddMinutes(5), TimeSpan.FromSeconds(1));
    }
}
