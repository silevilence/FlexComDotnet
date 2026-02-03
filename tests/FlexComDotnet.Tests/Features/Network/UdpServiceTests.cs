using System.Net;
using FlexComDotnet.Core.Features.Network.Models;
using FlexComDotnet.Core.Features.Network.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

public class UdpServiceTests
{
    [Fact]
    public void NewService_ShouldBeDisconnected()
    {
        // Arrange & Act
        using var service = new UdpService();

        // Assert
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Disconnected);
        service.CurrentConfig.Should().BeNull();
        service.ConnectionType.Should().Be(ConnectionType.Udp);
        service.LocalPort.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public async Task BindAsync_WithInvalidPort_ShouldReturnFalse(int port)
    {
        // Arrange
        using var service = new UdpService();
        var config = new UdpConfig { LocalPort = port };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.BindAsync(config);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("本地端口号无效");
    }

    [Fact]
    public async Task BindAsync_WithPortZero_ShouldBindToRandomPort()
    {
        // Arrange
        using var service = new UdpService();
        var config = new UdpConfig { LocalPort = 0 };

        // Act
        var result = await service.BindAsync(config);

        // Assert
        result.Should().BeTrue();
        service.IsConnected.Should().BeTrue();
        service.State.Should().Be(ConnectionState.Connected);
        service.LocalPort.Should().BeGreaterThan(0);

        // Cleanup
        await service.CloseAsync();
    }

    [Fact]
    public async Task BindAsync_WithSpecificPort_ShouldBindToThatPort()
    {
        // Arrange
        using var service = new UdpService();
        var port = GetAvailablePort();
        var config = new UdpConfig { LocalPort = port };

        // Act
        var result = await service.BindAsync(config);

        // Assert
        result.Should().BeTrue();
        service.LocalPort.Should().Be(port);

        // Cleanup
        await service.CloseAsync();
    }

    [Fact]
    public async Task BindAsync_WithEnableBroadcast_ShouldEnableBroadcast()
    {
        // Arrange
        using var service = new UdpService();
        var config = new UdpConfig
        {
            LocalPort = 0,
            EnableBroadcast = true
        };

        // Act
        var result = await service.BindAsync(config);

        // Assert
        result.Should().BeTrue();
        service.CurrentConfig!.EnableBroadcast.Should().BeTrue();

        // Cleanup
        await service.CloseAsync();
    }

    [Fact]
    public async Task CloseAsync_WhenBound_ShouldDisconnect()
    {
        // Arrange
        using var service = new UdpService();
        var config = new UdpConfig { LocalPort = 0 };
        await service.BindAsync(config);

        // Act
        await service.CloseAsync();

        // Assert
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Disconnected);
        service.CurrentConfig.Should().BeNull();
        service.LocalPort.Should().Be(0);
    }

    [Fact]
    public async Task CloseAsync_WhenNotBound_ShouldNotThrow()
    {
        // Arrange
        using var service = new UdpService();

        // Act & Assert
        await service.Invoking(s => s.CloseAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenAsync_WithoutConfig_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UdpService();
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.OpenAsync();

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("未设置配置");
    }

    [Fact]
    public async Task SendAsync_WhenNotBound_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UdpService();

        // Act
        var result = await service.SendAsync([0x01, 0x02, 0x03]);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendToAsync_WithEmptyHost_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UdpService();
        await service.BindAsync(new UdpConfig { LocalPort = 0 });
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.SendToAsync([0x01, 0x02], "", 8080);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("目标主机地址不能为空");

        // Cleanup
        await service.CloseAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task SendToAsync_WithInvalidPort_ShouldReturnFalse(int port)
    {
        // Arrange
        using var service = new UdpService();
        await service.BindAsync(new UdpConfig { LocalPort = 0 });
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.SendToAsync([0x01, 0x02], "127.0.0.1", port);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("目标端口号无效");

        // Cleanup
        await service.CloseAsync();
    }

    [Fact]
    public async Task SendToAsync_WithIPEndPoint_WhenNotBound_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UdpService();
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);

        // Act
        var result = await service.SendToAsync([0x01, 0x02], endPoint);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task BroadcastAsync_WhenBroadcastNotEnabled_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UdpService();
        await service.BindAsync(new UdpConfig { LocalPort = 0, EnableBroadcast = false });
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.BroadcastAsync([0x01, 0x02], 8080);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("广播功能未启用");

        // Cleanup
        await service.CloseAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task BroadcastAsync_WithInvalidPort_ShouldReturnFalse(int port)
    {
        // Arrange
        using var service = new UdpService();
        await service.BindAsync(new UdpConfig { LocalPort = 0, EnableBroadcast = true });
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.BroadcastAsync([0x01, 0x02], port);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("广播端口号无效");

        // Cleanup
        await service.CloseAsync();
    }

    [Fact]
    public async Task BroadcastAsync_WhenNotBound_ShouldReturnFalse()
    {
        // Arrange
        using var service = new UdpService();

        // Act
        var result = await service.BroadcastAsync([0x01, 0x02], 8080);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task StateChanged_ShouldBeRaisedOnStateChange()
    {
        // Arrange
        using var service = new UdpService();
        var stateChanges = new List<ConnectionState>();
        service.StateChanged += (_, state) => stateChanges.Add(state);

        // Act
        await service.BindAsync(new UdpConfig { LocalPort = 0 });

        // Assert
        stateChanges.Should().Contain(ConnectionState.Connected);

        // Cleanup
        await service.CloseAsync();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = new UdpService();

        // Act & Assert
        service.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new UdpService();

        // Act & Assert
        service.Invoking(s =>
        {
            s.Dispose();
            s.Dispose();
            s.Dispose();
        }).Should().NotThrow();
    }

    [Fact]
    public async Task BindAsync_Twice_ShouldRebind()
    {
        // Arrange
        using var service = new UdpService();
        var config1 = new UdpConfig { LocalPort = 0 };
        var config2 = new UdpConfig { LocalPort = 0, EnableBroadcast = true };

        // Act
        await service.BindAsync(config1);
        var firstPort = service.LocalPort;
        await service.BindAsync(config2);

        // Assert
        service.IsConnected.Should().BeTrue();
        service.CurrentConfig!.EnableBroadcast.Should().BeTrue();
        // 端口可能相同也可能不同，取决于系统

        // Cleanup
        await service.CloseAsync();
    }

    /// <summary>
    /// 获取一个可用的端口号
    /// </summary>
    private static int GetAvailablePort()
    {
        using var udp = new System.Net.Sockets.UdpClient(0);
        var port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        return port;
    }
}
