using System.Net;
using System.Net.Sockets;
using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// UDP 服务实现
/// </summary>
public class UdpService : IUdpService
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;
    private ConnectionState _state = ConnectionState.Disconnected;

    /// <inheritdoc/>
    public ConnectionType ConnectionType => ConnectionType.Udp;

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
    public bool IsConnected => State == ConnectionState.Connected;

    /// <inheritdoc/>
    public UdpConfig? CurrentConfig { get; private set; }

    /// <inheritdoc/>
    public int LocalPort { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<ConnectionState>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? ErrorOccurred;

    /// <inheritdoc/>
    public Task<bool> OpenAsync()
    {
        if (CurrentConfig == null)
        {
            RaiseError("未设置配置，请先调用 BindAsync");
            return Task.FromResult(false);
        }
        return BindAsync(CurrentConfig);
    }

    /// <inheritdoc/>
    public async Task<bool> BindAsync(UdpConfig config)
    {
        if (config.LocalPort < 0 || config.LocalPort > 65535)
        {
            RaiseError("本地端口号无效，必须在 0-65535 范围内");
            return false;
        }

        try
        {
            await CloseAsync();

            _udpClient = new UdpClient(config.LocalPort)
            {
                EnableBroadcast = config.EnableBroadcast
            };

            // 设置接收缓冲区大小
            _udpClient.Client.ReceiveBufferSize = config.ReceiveBufferSize;

            // 获取实际绑定的端口
            LocalPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;

            CurrentConfig = config.Clone();
            State = ConnectionState.Connected;

            // 启动接收任务
            _receiveCts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

            return true;
        }
        catch (SocketException ex)
        {
            State = ConnectionState.Error;
            RaiseError($"绑定端口失败: {ex.Message} (错误码: {ex.SocketErrorCode})");
            await CloseInternalAsync();
            return false;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            RaiseError($"绑定端口失败: {ex.Message}");
            await CloseInternalAsync();
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync()
    {
        await CloseInternalAsync();
        State = ConnectionState.Disconnected;
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(byte[] data)
    {
        if (!IsConnected || _udpClient == null || CurrentConfig == null)
        {
            return false;
        }

        return await SendToAsync(data, CurrentConfig.RemoteHost, CurrentConfig.RemotePort);
    }

    /// <inheritdoc/>
    public async Task<bool> SendToAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        if (!IsConnected || _udpClient == null)
        {
            return false;
        }

        try
        {
            await _udpClient.SendAsync(data, data.Length, remoteEndPoint);
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"发送数据失败: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendToAsync(byte[] data, string host, int port)
    {
        if (!IsConnected || _udpClient == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            RaiseError("目标主机地址不能为空");
            return false;
        }

        if (port < 1 || port > 65535)
        {
            RaiseError("目标端口号无效");
            return false;
        }

        try
        {
            await _udpClient.SendAsync(data, data.Length, host, port);
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"发送数据失败: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> BroadcastAsync(byte[] data, int port)
    {
        if (!IsConnected || _udpClient == null)
        {
            return false;
        }

        if (!_udpClient.EnableBroadcast)
        {
            RaiseError("广播功能未启用");
            return false;
        }

        if (port < 1 || port > 65535)
        {
            RaiseError("广播端口号无效");
            return false;
        }

        try
        {
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);
            await _udpClient.SendAsync(data, data.Length, broadcastEndPoint);
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"广播数据失败: {ex.Message}");
            return false;
        }
    }

    private async Task CloseInternalAsync()
    {
        // 取消接收任务
        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        // 等待接收任务完成
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // 忽略取消异常
            }
            _receiveTask = null;
        }

        // 关闭 UDP 客户端
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;

        CurrentConfig = null;
        LocalPort = 0;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _udpClient != null)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udpClient.ReceiveAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    // UDP 客户端已关闭
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // UDP 客户端已释放
                    break;
                }

                DataReceived?.Invoke(this, new DataReceivedEventArgs(result.Buffer, result.RemoteEndPoint));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseError($"接收数据时出错: {ex.Message}");
        }
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
            CloseAsync().GetAwaiter().GetResult();
        }

        _disposed = true;
    }
}
