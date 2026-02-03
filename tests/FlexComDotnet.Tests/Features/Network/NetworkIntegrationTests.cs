using System.Net;
using System.Net.Sockets;
using FlexComDotnet.Core.Features.Network.Models;
using FlexComDotnet.Core.Features.Network.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Network;

/// <summary>
/// 网络服务集成测试 - 测试实际的网络通信
/// </summary>
public class NetworkIntegrationTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try { disposable.Dispose(); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    #region TCP Client/Server Integration Tests

    [Fact]
    public async Task TcpClientServer_ShouldCommunicate()
    {
        // Arrange
        var server = new TcpServerService();
        var client = new TcpClientService();
        _disposables.Add(server);
        _disposables.Add(client);

        var port = GetAvailableTcpPort();
        var serverReceived = new List<byte[]>();
        var clientReceived = new List<byte[]>();
        var clientConnectedEvent = new TaskCompletionSource<ClientInfo>();

        server.DataReceived += (_, args) => serverReceived.Add(args.Data);
        server.ClientConnected += (_, info) => clientConnectedEvent.TrySetResult(info);
        client.DataReceived += (_, args) => clientReceived.Add(args.Data);

        // Start server
        var serverStarted = await server.StartAsync(new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = port
        });
        serverStarted.Should().BeTrue();

        // Connect client
        var clientConnected = await client.ConnectAsync(new TcpClientConfig
        {
            Host = "127.0.0.1",
            Port = port,
            ConnectTimeout = 5000
        });
        clientConnected.Should().BeTrue();

        // Wait for client to be registered on server
        var connectedClient = await clientConnectedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        connectedClient.Should().NotBeNull();

        // Act - Client sends to Server
        var testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var sendResult = await client.SendAsync(testData);
        sendResult.Should().BeTrue();

        // Wait for data to arrive
        await Task.Delay(100);

        // Assert - Server received data
        serverReceived.Should().HaveCount(1);
        serverReceived[0].Should().BeEquivalentTo(testData);

        // Act - Server sends to Client
        var responseData = new byte[] { 0xAA, 0xBB, 0xCC };
        var broadcastResult = await server.BroadcastAsync(responseData);
        broadcastResult.Should().Be(1);

        // Wait for data to arrive
        await Task.Delay(100);

        // Assert - Client received data
        clientReceived.Should().HaveCount(1);
        clientReceived[0].Should().BeEquivalentTo(responseData);

        // Cleanup
        await client.CloseAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task TcpServer_ShouldHandleMultipleClients()
    {
        // Arrange
        var server = new TcpServerService();
        _disposables.Add(server);

        var port = GetAvailableTcpPort();
        var connectedClients = new List<ClientInfo>();
        var disconnectedClients = new List<ClientInfo>();

        server.ClientConnected += (_, info) => connectedClients.Add(info);
        server.ClientDisconnected += (_, info) => disconnectedClients.Add(info);

        await server.StartAsync(new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = port,
            MaxConnections = 5
        });

        // Act - Connect multiple clients
        var clients = new List<TcpClientService>();
        for (int i = 0; i < 3; i++)
        {
            var client = new TcpClientService();
            _disposables.Add(client);
            clients.Add(client);

            await client.ConnectAsync(new TcpClientConfig
            {
                Host = "127.0.0.1",
                Port = port
            });
        }

        // Wait for connections to be registered
        await Task.Delay(200);

        // Assert
        connectedClients.Should().HaveCount(3);
        server.ConnectedClients.Should().HaveCount(3);

        // Disconnect one client
        await clients[0].CloseAsync();
        await Task.Delay(100);

        disconnectedClients.Should().HaveCount(1);
        server.ConnectedClients.Should().HaveCount(2);

        // Cleanup
        foreach (var client in clients.Skip(1))
        {
            await client.CloseAsync();
        }
        await server.StopAsync();
    }

    [Fact]
    public async Task TcpServer_ShouldEnforceMaxConnections()
    {
        // Arrange
        var server = new TcpServerService();
        _disposables.Add(server);

        var port = GetAvailableTcpPort();

        await server.StartAsync(new TcpServerConfig
        {
            ListenAddress = "127.0.0.1",
            Port = port,
            MaxConnections = 2
        });

        // Act - Try to connect more than max
        var clients = new List<TcpClientService>();
        for (int i = 0; i < 3; i++)
        {
            var client = new TcpClientService();
            _disposables.Add(client);
            clients.Add(client);

            await client.ConnectAsync(new TcpClientConfig
            {
                Host = "127.0.0.1",
                Port = port
            });
        }

        await Task.Delay(200);

        // Assert - Only 2 clients should be connected
        server.ConnectedClients.Should().HaveCountLessThanOrEqualTo(2);

        // Cleanup
        foreach (var client in clients)
        {
            await client.CloseAsync();
        }
        await server.StopAsync();
    }

    #endregion

    #region UDP Integration Tests

    [Fact]
    public async Task Udp_ShouldSendAndReceive()
    {
        // Arrange
        var sender = new UdpService();
        var receiver = new UdpService();
        _disposables.Add(sender);
        _disposables.Add(receiver);

        var receiverPort = GetAvailableUdpPort();
        var receivedData = new TaskCompletionSource<DataReceivedEventArgs>();

        receiver.DataReceived += (_, args) => receivedData.TrySetResult(args);

        await receiver.BindAsync(new UdpConfig { LocalPort = receiverPort });
        await sender.BindAsync(new UdpConfig { LocalPort = 0 });

        // Act
        var testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var sendResult = await sender.SendToAsync(testData, "127.0.0.1", receiverPort);
        sendResult.Should().BeTrue();

        // Wait for data with timeout
        var received = await receivedData.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        received.Data.Should().BeEquivalentTo(testData);
        received.RemoteEndPoint.Should().NotBeNull();

        // Cleanup
        await sender.CloseAsync();
        await receiver.CloseAsync();
    }

    [Fact]
    public async Task Udp_ShouldSendUsingDefaultRemote()
    {
        // Arrange
        var sender = new UdpService();
        var receiver = new UdpService();
        _disposables.Add(sender);
        _disposables.Add(receiver);

        var receiverPort = GetAvailableUdpPort();
        var receivedData = new TaskCompletionSource<DataReceivedEventArgs>();

        receiver.DataReceived += (_, args) => receivedData.TrySetResult(args);

        await receiver.BindAsync(new UdpConfig { LocalPort = receiverPort });
        await sender.BindAsync(new UdpConfig
        {
            LocalPort = 0,
            RemoteHost = "127.0.0.1",
            RemotePort = receiverPort
        });

        // Act - Use SendAsync which uses default remote
        var testData = new byte[] { 0xAA, 0xBB, 0xCC };
        var sendResult = await sender.SendAsync(testData);
        sendResult.Should().BeTrue();

        // Wait for data
        var received = await receivedData.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        received.Data.Should().BeEquivalentTo(testData);

        // Cleanup
        await sender.CloseAsync();
        await receiver.CloseAsync();
    }

    [Fact]
    public async Task Udp_ShouldSendToIPEndPoint()
    {
        // Arrange
        var sender = new UdpService();
        var receiver = new UdpService();
        _disposables.Add(sender);
        _disposables.Add(receiver);

        var receiverPort = GetAvailableUdpPort();
        var receivedData = new TaskCompletionSource<DataReceivedEventArgs>();

        receiver.DataReceived += (_, args) => receivedData.TrySetResult(args);

        await receiver.BindAsync(new UdpConfig { LocalPort = receiverPort });
        await sender.BindAsync(new UdpConfig { LocalPort = 0 });

        // Act
        var testData = new byte[] { 0x11, 0x22, 0x33 };
        var endPoint = new IPEndPoint(IPAddress.Loopback, receiverPort);
        var sendResult = await sender.SendToAsync(testData, endPoint);
        sendResult.Should().BeTrue();

        // Wait for data
        var received = await receivedData.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        received.Data.Should().BeEquivalentTo(testData);

        // Cleanup
        await sender.CloseAsync();
        await receiver.CloseAsync();
    }

    #endregion

    #region Helper Methods

    private static int GetAvailableTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static int GetAvailableUdpPort()
    {
        using var udp = new UdpClient(0);
        var port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        return port;
    }

    #endregion
}
