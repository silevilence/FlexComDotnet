using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// TCP 服务器服务实现
/// </summary>
public class TcpServerService : ITcpServerService
{
    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private Task? _acceptTask;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
    private bool _disposed;
    private ConnectionState _state = ConnectionState.Disconnected;
    private int _clientIdCounter;

    private const int ReceiveBufferSize = 8192;

    /// <inheritdoc/>
    public ConnectionType ConnectionType => ConnectionType.TcpServer;

    /// <inheritdoc/>
    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsConnected => State == ConnectionState.Listening;

    /// <inheritdoc/>
    public TcpServerConfig? CurrentConfig { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<ClientInfo> ConnectedClients =>
        _clients.Values.Select(c => c.Info).ToList();

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<ConnectionState>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? ErrorOccurred;

    /// <inheritdoc/>
    public event EventHandler<ClientInfo>? ClientConnected;

    /// <inheritdoc/>
    public event EventHandler<ClientInfo>? ClientDisconnected;

    /// <inheritdoc/>
    public Task<bool> OpenAsync()
    {
        if (CurrentConfig == null)
        {
            RaiseError("未设置配置，请先调用 StartAsync");
            return Task.FromResult(false);
        }
        return StartAsync(CurrentConfig);
    }

    /// <inheritdoc/>
    public async Task<bool> StartAsync(TcpServerConfig config)
    {
        if (config.Port < 1 || config.Port > 65535)
        {
            RaiseError("端口号无效，必须在 1-65535 范围内");
            return false;
        }

        try
        {
            await StopAsync();

            var listenAddress = IPAddress.Parse(config.ListenAddress);
            _listener = new TcpListener(listenAddress, config.Port);
            _listener.Start(config.Backlog);

            CurrentConfig = config.Clone();
            State = ConnectionState.Listening;

            // 启动接受连接任务
            _listenerCts = new CancellationTokenSource();
            _acceptTask = AcceptClientsLoopAsync(_listenerCts.Token);

            return true;
        }
        catch (SocketException ex)
        {
            State = ConnectionState.Error;
            RaiseError($"启动服务器失败: {ex.Message} (错误码: {ex.SocketErrorCode})");
            await StopInternalAsync();
            return false;
        }
        catch (FormatException)
        {
            RaiseError($"无效的监听地址: {config.ListenAddress}");
            return false;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            RaiseError($"启动服务器失败: {ex.Message}");
            await StopInternalAsync();
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync()
    {
        await StopAsync();
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await StopInternalAsync();
        State = ConnectionState.Disconnected;
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(byte[] data)
    {
        // 向所有客户端广播
        var count = await BroadcastAsync(data);
        return count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> SendToClientAsync(string clientId, byte[] data)
    {
        if (!_clients.TryGetValue(clientId, out var client))
        {
            return false;
        }

        try
        {
            if (client.Stream == null || !client.TcpClient.Connected)
            {
                return false;
            }

            await client.Stream.WriteAsync(data);
            await client.Stream.FlushAsync();
            client.Info.SentBytes += data.Length;
            client.Info.LastActivityTime = DateTime.Now;
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"向客户端 {clientId} 发送数据失败: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> BroadcastAsync(byte[] data)
    {
        var successCount = 0;
        var tasks = _clients.Keys.Select(async clientId =>
        {
            if (await SendToClientAsync(clientId, data))
            {
                Interlocked.Increment(ref successCount);
            }
        });

        await Task.WhenAll(tasks);
        return successCount;
    }

    /// <inheritdoc/>
    public async Task DisconnectClientAsync(string clientId)
    {
        if (_clients.TryRemove(clientId, out var client))
        {
            await CloseClientAsync(client);
            ClientDisconnected?.Invoke(this, client.Info);
        }
    }

    private async Task StopInternalAsync()
    {
        // 取消监听任务
        if (_listenerCts != null)
        {
            await _listenerCts.CancelAsync();
            _listenerCts.Dispose();
            _listenerCts = null;
        }

        // 停止监听器
        _listener?.Stop();
        _listener = null;

        // 等待接受任务完成
        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask;
            }
            catch
            {
                // 忽略取消异常
            }
            _acceptTask = null;
        }

        // 关闭所有客户端连接
        foreach (var clientId in _clients.Keys.ToList())
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                await CloseClientAsync(client);
                ClientDisconnected?.Invoke(this, client.Info);
            }
        }

        CurrentConfig = null;
    }

    private async Task AcceptClientsLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // 监听器已关闭
                    break;
                }

                // 检查最大连接数
                if (CurrentConfig != null && _clients.Count >= CurrentConfig.MaxConnections)
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                    continue;
                }

                // 创建客户端连接
                var clientId = $"client-{Interlocked.Increment(ref _clientIdCounter)}";
                var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                var clientInfo = new ClientInfo(clientId, remoteEndPoint);
                var connection = new ClientConnection(clientId, tcpClient, clientInfo);

                if (_clients.TryAdd(clientId, connection))
                {
                    ClientConnected?.Invoke(this, clientInfo);

                    // 启动接收任务
                    _ = ReceiveFromClientAsync(connection, cancellationToken);
                }
                else
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseError($"接受连接时出错: {ex.Message}");
        }
    }

    private async Task ReceiveFromClientAsync(ClientConnection client, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Stream != null && client.TcpClient.Connected)
            {
                int bytesRead;
                try
                {
                    bytesRead = await client.Stream.ReadAsync(buffer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // 连接断开
                    break;
                }

                if (bytesRead == 0)
                {
                    // 客户端关闭了连接
                    break;
                }

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                client.Info.ReceivedBytes += bytesRead;
                client.Info.LastActivityTime = DateTime.Now;

                DataReceived?.Invoke(this, new DataReceivedEventArgs(data, client.Info.RemoteEndPoint));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseError($"从客户端 {client.Id} 接收数据时出错: {ex.Message}");
        }
        finally
        {
            // 客户端断开
            if (_clients.TryRemove(client.Id, out _))
            {
                await CloseClientAsync(client);
                ClientDisconnected?.Invoke(this, client.Info);
            }
        }
    }

    private static Task CloseClientAsync(ClientConnection client)
    {
        try
        {
            client.Stream?.Close();
            client.TcpClient.Close();
            client.TcpClient.Dispose();
        }
        catch
        {
            // 忽略关闭错误
        }
        return Task.CompletedTask;
    }

    private void RaiseError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        _disposed = true;
    }

    /// <summary>
    /// 客户端连接内部类
    /// </summary>
    private sealed class ClientConnection
    {
        public string Id { get; }
        public TcpClient TcpClient { get; }
        public NetworkStream? Stream { get; }
        public ClientInfo Info { get; }

        public ClientConnection(string id, TcpClient tcpClient, ClientInfo info)
        {
            Id = id;
            TcpClient = tcpClient;
            Stream = tcpClient.GetStream();
            Info = info;
        }
    }
}
