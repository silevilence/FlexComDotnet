using FlexComDotnet.Core.Features.Network.Models;
using FlexComDotnet.Core.Features.Network.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

public class TcpClientServiceTests
{
    [Fact]
    public void NewService_ShouldBeDisconnected()
    {
        // Arrange & Act
        using var service = new TcpClientService();

        // Assert
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Disconnected);
        service.CurrentConfig.Should().BeNull();
        service.ConnectionType.Should().Be(ConnectionType.TcpClient);
    }

    [Fact]
    public async Task ConnectAsync_WithEmptyHost_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpClientService();
        var config = new TcpClientConfig { Host = "", Port = 8080 };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
        errorMessage.Should().Contain("主机地址不能为空");
    }

    [Fact]
    public async Task ConnectAsync_WithWhitespaceHost_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpClientService();
        var config = new TcpClientConfig { Host = "   ", Port = 8080 };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("主机地址不能为空");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public async Task ConnectAsync_WithInvalidPort_ShouldReturnFalse(int port)
    {
        // Arrange
        using var service = new TcpClientService();
        var config = new TcpClientConfig { Host = "127.0.0.1", Port = port };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("端口号无效");
    }

    [Fact]
    public async Task ConnectAsync_WithUnreachableHost_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpClientService();
        var config = new TcpClientConfig 
        { 
            Host = "192.0.2.1", // 文档保留地址，不可路由
            Port = 9999,
            ConnectTimeout = 500 // 短超时
        };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Error);
    }

    [Fact]
    public async Task ConnectAsync_WithConnectionRefused_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpClientService();
        var config = new TcpClientConfig
        {
            Host = "127.0.0.1",
            Port = 59999, // 不太可能有服务在监听的端口
            ConnectTimeout = 1000
        };
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
        // 应该是 Error 状态或回到 Disconnected
    }

    [Fact]
    public async Task OpenAsync_WithoutConfig_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpClientService();
        string? errorMessage = null;
        service.ErrorOccurred += (_, msg) => errorMessage = msg;

        // Act
        var result = await service.OpenAsync();

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("未设置配置");
    }

    [Fact]
    public async Task CloseAsync_WhenNotConnected_ShouldNotThrow()
    {
        // Arrange
        using var service = new TcpClientService();

        // Act & Assert
        await service.Invoking(s => s.CloseAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TcpClientService();

        // Act
        var result = await service.SendAsync([0x01, 0x02, 0x03]);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task StateChanged_ShouldBeRaisedOnStateChange()
    {
        // Arrange
        using var service = new TcpClientService();
        var stateChanges = new List<ConnectionState>();
        service.StateChanged += (_, state) => stateChanges.Add(state);

        // Act - 尝试连接到不存在的服务器
        await service.ConnectAsync(new TcpClientConfig
        {
            Host = "127.0.0.1",
            Port = 59998,
            ConnectTimeout = 100
        });

        // Assert - 应该至少触发 Connecting 状态
        stateChanges.Should().Contain(ConnectionState.Connecting);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = new TcpClientService();

        // Act & Assert
        service.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new TcpClientService();

        // Act & Assert
        service.Invoking(s =>
        {
            s.Dispose();
            s.Dispose();
            s.Dispose();
        }).Should().NotThrow();
    }
}
