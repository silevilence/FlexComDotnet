using FlexComDotnet.Core.Features.Network.Models;
using FlexComDotnet.Core.Features.Network.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

public class TcpServerServiceTests
{
    [Fact]
    public void NewService_ShouldBeDisconnected()
    {
        // Arrange & Act
        using var service = new TcpServerService();

        // Assert
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Disconnected);
        service.CurrentConfig.Should().BeNull();
        service.ConnectionType.Should().Be(ConnectionType.TcpServer);
        service.ConnectedClients.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public async Task StartAsync_WithInvalidPort_ShouldReturnFalse(int port)
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig { Port = port };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.StartAsync(config);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("端口号无效");
    }

    [Fact]
    public async Task StartAsync_WithInvalidListenAddress_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig 
        { 
            ListenAddress = "invalid-address",
            Port = 8080 
        };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.StartAsync(config);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("无效的监听地址");
    }

    [Fact]
    public async Task StartAsync_WithValidConfig_ShouldStartListening()
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = GetAvailablePort(),
            MaxConnections = 5,
            Backlog = 10
        };
        var stateChanges = new List<ConnectionState>();
        service.StateChanged += (_, state) => stateChanges.Add(state);

        // Act
        var result = await service.StartAsync(config);

        // Assert
        result.Should().BeTrue();
        service.IsConnected.Should().BeTrue();
        service.State.Should().Be(ConnectionState.Listening);
        service.CurrentConfig.Should().NotBeNull();
        service.CurrentConfig!.Port.Should().Be(config.Port);
        stateChanges.Should().Contain(ConnectionState.Listening);

        // Cleanup
        await service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WhenListening_ShouldStopAndDisconnect()
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = GetAvailablePort()
        };
        await service.StartAsync(config);

        // Act
        await service.StopAsync();

        // Assert
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Disconnected);
        service.CurrentConfig.Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_ShouldNotThrow()
    {
        // Arrange
        using var service = new TcpServerService();

        // Act & Assert
        await service.Invoking(s => s.StopAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenAsync_WithoutConfig_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpServerService();
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.OpenAsync();

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("未设置配置");
    }

    [Fact]
    public async Task SendAsync_WhenNoClients_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = GetAvailablePort()
        };
        await service.StartAsync(config);

        // Act
        var result = await service.SendAsync([0x01, 0x02, 0x03]);

        // Assert
        result.Should().BeFalse(); // 没有客户端连接

        // Cleanup
        await service.StopAsync();
    }

    [Fact]
    public async Task SendToClientAsync_WithInvalidClientId_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = GetAvailablePort()
        };
        await service.StartAsync(config);

        // Act
        var result = await service.SendToClientAsync("non-existent-client", [0x01, 0x02]);

        // Assert
        result.Should().BeFalse();

        // Cleanup
        await service.StopAsync();
    }

    [Fact]
    public async Task DisconnectClientAsync_WithInvalidClientId_ShouldNotThrow()
    {
        // Arrange
        using var service = new TcpServerService();
        var config = new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = GetAvailablePort()
        };
        await service.StartAsync(config);

        // Act & Assert
        await service.Invoking(s => s.DisconnectClientAsync("non-existent"))
            .Should().NotThrowAsync();

        // Cleanup
        await service.StopAsync();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = new TcpServerService();

        // Act & Assert
        service.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new TcpServerService();

        // Act & Assert
        service.Invoking(s =>
        {
            s.Dispose();
            s.Dispose();
            s.Dispose();
        }).Should().NotThrow();
    }

    [Fact]
    public async Task StartAsync_Twice_ShouldRestartServer()
    {
        // Arrange
        using var service = new TcpServerService();
        var port1 = GetAvailablePort();
        var port2 = GetAvailablePort();
        
        var config1 = new TcpServerConfig { ListenAddress = "127.0.0.1", Port = port1 };
        var config2 = new TcpServerConfig { ListenAddress = "127.0.0.1", Port = port2 };

        // Act
        var result1 = await service.StartAsync(config1);
        var result2 = await service.StartAsync(config2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        service.CurrentConfig!.Port.Should().Be(port2);

        // Cleanup
        await service.StopAsync();
    }

    /// <summary>
    /// 获取一个可用的端口号
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
