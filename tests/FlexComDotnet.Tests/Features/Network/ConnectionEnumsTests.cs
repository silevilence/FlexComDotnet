using FlexComDotnet.Core.Features.Network.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

public class ConnectionTypeTests
{
    [Fact]
    public void ConnectionType_ShouldContainAllExpectedValues()
    {
        // Assert
        Enum.GetValues<ConnectionType>().Should().HaveCount(4);
        Enum.IsDefined(ConnectionType.Serial).Should().BeTrue();
        Enum.IsDefined(ConnectionType.TcpClient).Should().BeTrue();
        Enum.IsDefined(ConnectionType.TcpServer).Should().BeTrue();
        Enum.IsDefined(ConnectionType.Udp).Should().BeTrue();
    }
}

public class ConnectionStateTests
{
    [Fact]
    public void ConnectionState_ShouldContainAllExpectedValues()
    {
        // Assert
        Enum.GetValues<ConnectionState>().Should().HaveCount(5);
        Enum.IsDefined(ConnectionState.Disconnected).Should().BeTrue();
        Enum.IsDefined(ConnectionState.Connecting).Should().BeTrue();
        Enum.IsDefined(ConnectionState.Connected).Should().BeTrue();
        Enum.IsDefined(ConnectionState.Listening).Should().BeTrue();
        Enum.IsDefined(ConnectionState.Error).Should().BeTrue();
    }
}
