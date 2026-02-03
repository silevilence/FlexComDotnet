using System.Net.Sockets;
using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Core.Features.Network.Services;

/// <summary>
/// TCP 客户端服务实现
/// </summary>
public class TcpClientService : ITcpClientService
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;
    private ConnectionState _state = ConnectionState.Disconnected;

    private const int ReceiveBufferSize = 8192;

    /// <inheritdoc/>
    public ConnectionType ConnectionType => ConnectionType.TcpClient;

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
    public bool IsConnected => _tcpClient?.Connected == true && State == ConnectionState.Connected;

    /// <inheritdoc/>
    public TcpClientConfig? CurrentConfig { get; private set; }

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
            RaiseError("未设置配置，请先调用 ConnectAsync");
            return Task.FromResult(false);
        }
        return ConnectAsync(CurrentConfig);
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(TcpClientConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            RaiseError("主机地址不能为空");
            return false;
        }

        if (config.Port < 1 || config.Port > 65535)
        {
            RaiseError("端口号无效，必须在 1-65535 范围内");
            return false;
        }

        try
        {
            await CloseAsync();

            State = ConnectionState.Connecting;

            _tcpClient = new TcpClient
            {
                NoDelay = config.NoDelay,
                ReceiveTimeout = config.ReceiveTimeout,
                SendTimeout = config.SendTimeout
            };

            if (config.KeepAlive)
            {
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }

            // 使用超时连接
            using var connectCts = new CancellationTokenSource(config.ConnectTimeout);
            try
            {
                await _tcpClient.ConnectAsync(config.Host, config.Port, connectCts.Token);
            }
            catch (OperationCanceledException)
            {
                State = ConnectionState.Error;
                RaiseError($"连接超时 ({config.ConnectTimeout}ms)");
                await CloseInternalAsync();
                return false;
            }

            _networkStream = _tcpClient.GetStream();
            CurrentConfig = config.Clone();
            State = ConnectionState.Connected;

            // 启动接收线程
            _receiveCts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

            return true;
        }
        catch (SocketException ex)
        {
            State = ConnectionState.Error;
            RaiseError($"连接失败: {ex.Message} (错误码: {ex.SocketErrorCode})");
            await CloseInternalAsync();
            return false;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            RaiseError($"连接失败: {ex.Message}");
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
        if (!IsConnected || _networkStream == null)
        {
            return false;
        }

        try
        {
            await _networkStream.WriteAsync(data);
            await _networkStream.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"发送数据失败: {ex.Message}");
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

        // 关闭流和客户端
        _networkStream?.Close();
        _networkStream = null;

        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpClient = null;

        CurrentConfig = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _networkStream != null && _tcpClient?.Connected == true)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _networkStream.ReadAsync(buffer, cancellationToken);
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
                    // 服务器关闭了连接
                    break;
                }

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                var remoteEndPoint = _tcpClient?.Client.RemoteEndPoint as System.Net.IPEndPoint;
                DataReceived?.Invoke(this, new DataReceivedEventArgs(data, remoteEndPoint));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseError($"接收数据时出错: {ex.Message}");
        }
        finally
        {
            // 如果循环结束但还在连接状态，说明是远程断开
            if (State == ConnectionState.Connected)
            {
                State = ConnectionState.Disconnected;
            }
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
