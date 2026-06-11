using System.IO.Ports;
using System.Management;
using System.Text;
using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 串口服务实现
/// </summary>
public class SerialPortService : ISerialPortService, IDisposable
{
    private readonly object _lock = new();
    private SerialPort? _serialPort;
    private bool _disposed;
    private FrameDelimiter? _frameDelimiter;
    private CancellationTokenSource? _flushDebounceCts;

    /// <inheritdoc/>
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    /// <inheritdoc/>
    public SerialPortConfig? CurrentConfig { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<byte[]>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<byte[]>? FrameReceived;

    /// <inheritdoc/>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? ErrorOccurred;

    /// <inheritdoc/>
    public event EventHandler<HookProcessedEventArgs>? HookProcessed;

    /// <inheritdoc/>
    public Func<byte[], byte[]>? RxPreProcessor { get; set; }

    /// <inheritdoc/>
    public Func<byte[], byte[]>? TxPostProcessor { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<SerialPortInfo> GetAvailablePorts()
    {
        var portInfos = new List<SerialPortInfo>();
        var portNames = SerialPort.GetPortNames();

        // 尝试获取串口描述信息 (仅Windows)
        var portDescriptions = GetPortDescriptions();

        foreach (var portName in portNames.OrderBy(p => p))
        {
            portInfos.Add(new SerialPortInfo
            {
                PortName = portName,
                Description = portDescriptions.GetValueOrDefault(portName, string.Empty)
            });
        }

        return portInfos;
    }

    /// <inheritdoc/>
    public bool Open(SerialPortConfig config)
    {
        if (string.IsNullOrEmpty(config.PortName))
        {
            RaiseError("串口名称不能为空");
            return false;
        }

        try
        {
            Close();

            _serialPort = new SerialPort
            {
                PortName = config.PortName,
                BaudRate = (int)config.BaudRate,
                DataBits = (int)config.DataBits,
                StopBits = ConvertStopBits(config.StopBits),
                Parity = ConvertParity(config.Parity),
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            // 配置流控
            ConfigureFlowControl(_serialPort, config.FlowControl);

            _serialPort.DataReceived += OnSerialDataReceived;
            _serialPort.ErrorReceived += OnSerialErrorReceived;

            _serialPort.Open();
            CurrentConfig = config.Clone();

            // 初始化帧定界器
            _frameDelimiter = new FrameDelimiter(config.FrameIntervalMs, config.MaxFrameBytes);
            _frameDelimiter.FrameCompleted += OnFrameCompleted;

            ConnectionStateChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"打开串口失败: {ex.Message}");
            _serialPort?.Dispose();
            _serialPort = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        if (_serialPort == null) return;

        // Flush 残留数据
        _flushDebounceCts?.Cancel();
        _flushDebounceCts?.Dispose();
        _flushDebounceCts = null;

        lock (_lock)
        {
            _frameDelimiter?.Flush();
            _frameDelimiter = null;
        }

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.DataReceived -= OnSerialDataReceived;
                _serialPort.ErrorReceived -= OnSerialErrorReceived;
                _serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            RaiseError($"关闭串口时出错: {ex.Message}");
        }
        finally
        {
            _serialPort?.Dispose();
            _serialPort = null;
            CurrentConfig = null;
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    /// <inheritdoc/>
    public bool Send(byte[] data)
    {
        if (!IsConnected || _serialPort == null)
        {
            return false;
        }

        try
        {
            var dataToSend = data;

            // 应用 Tx 后处理器
            if (TxPostProcessor != null)
            {
                dataToSend = TxPostProcessor(data);
                
                // 如果数据被修改，触发事件
                if (!data.SequenceEqual(dataToSend))
                {
                    HookProcessed?.Invoke(this, new HookProcessedEventArgs(data, dataToSend, isTx: true));
                }
            }

            _serialPort.Write(dataToSend, 0, dataToSend.Length);
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"发送数据失败: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Send(string text)
    {
        return Send(Encoding.UTF8.GetBytes(text));
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
            Close();
        }

        _disposed = true;
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null || !_serialPort.IsOpen) return;

        try
        {
            var bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead > 0)
            {
                var buffer = new byte[bytesToRead];
                _serialPort.Read(buffer, 0, bytesToRead);
                var originalBuffer = buffer;

                // 应用 Rx 预处理器
                if (RxPreProcessor != null)
                {
                    buffer = RxPreProcessor(originalBuffer);
                    
                    // 如果数据被修改，触发事件
                    if (!originalBuffer.SequenceEqual(buffer))
                    {
                        HookProcessed?.Invoke(this, new HookProcessedEventArgs(originalBuffer, buffer, isTx: false));
                    }
                }

                DataReceived?.Invoke(this, buffer);

                // 通过 FrameDelimiter 逐字节处理
                var now = DateTime.UtcNow;
                lock (_lock)
                {
                    foreach (var b in buffer)
                    {
                        _frameDelimiter?.AppendByte(b, now);
                    }

                    // 数据流停止后经过一个帧间隔自动产出残留帧
                    ResetFlushDebounce();
                }
            }
        }
        catch (Exception ex)
        {
            RaiseError($"读取数据时出错: {ex.Message}");
        }
    }

    private void OnSerialErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        RaiseError($"串口错误: {e.EventType}");
    }

    private void OnFrameCompleted(byte[] frame)
    {
        FrameReceived?.Invoke(this, frame);
    }

    /// <summary>
    /// 重置 Flush 防抖定时器 — 每次收到数据块后调用，取消之前的等待并重新计时
    /// 必须在 lock (_lock) 内调用
    /// </summary>
    private void ResetFlushDebounce()
    {
        _flushDebounceCts?.Cancel();
        _flushDebounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _flushDebounceCts = cts;

        // 获取当前 FrameIntervalMs（从 currentConfig 或默认值）
        var intervalMs = CurrentConfig?.FrameIntervalMs ?? 10;
        _ = DebounceFlushAsync(intervalMs, cts.Token);
    }

    private async Task DebounceFlushAsync(int intervalMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(intervalMs, ct);
        }
        catch (TaskCanceledException)
        {
            return; // 有新数据到达，放弃 flush
        }

        // 在锁内取出帧数据，在锁外触发事件，避免订阅者占用锁
        byte[]? frame;
        lock (_lock)
        {
            frame = _frameDelimiter?.TryFlush();
        }

        if (frame != null && frame.Length > 0)
        {
            FrameReceived?.Invoke(this, frame);
        }
    }

    private void RaiseError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
    }

    private static System.IO.Ports.StopBits ConvertStopBits(StopBitsOption option) => option switch
    {
        StopBitsOption.One => System.IO.Ports.StopBits.One,
        StopBitsOption.OnePointFive => System.IO.Ports.StopBits.OnePointFive,
        StopBitsOption.Two => System.IO.Ports.StopBits.Two,
        _ => System.IO.Ports.StopBits.One
    };

    private static System.IO.Ports.Parity ConvertParity(ParityOption option) => option switch
    {
        ParityOption.None => System.IO.Ports.Parity.None,
        ParityOption.Odd => System.IO.Ports.Parity.Odd,
        ParityOption.Even => System.IO.Ports.Parity.Even,
        ParityOption.Mark => System.IO.Ports.Parity.Mark,
        ParityOption.Space => System.IO.Ports.Parity.Space,
        _ => System.IO.Ports.Parity.None
    };

    private static void ConfigureFlowControl(SerialPort port, FlowControlOption option)
    {
        switch (option)
        {
            case FlowControlOption.None:
                port.Handshake = Handshake.None;
                port.RtsEnable = false;
                port.DtrEnable = false;
                break;
            case FlowControlOption.XonXoff:
                port.Handshake = Handshake.XOnXOff;
                break;
            case FlowControlOption.RtsCts:
                port.Handshake = Handshake.RequestToSend;
                break;
            case FlowControlOption.DtrDsr:
                // DTR/DSR 需要手动控制
                port.Handshake = Handshake.None;
                port.DtrEnable = true;
                break;
        }
    }

    /// <summary>
    /// 获取串口描述信息 (通过WMI, 仅Windows)
    /// </summary>
    private static Dictionary<string, string> GetPortDescriptions()
    {
        var descriptions = new Dictionary<string, string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");
            
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;

                // 从名称中提取 COM 端口号
                var match = System.Text.RegularExpressions.Regex.Match(name, @"\((COM\d+)\)");
                if (match.Success)
                {
                    var portName = match.Groups[1].Value;
                    // 移除括号中的端口号，获取描述
                    var description = name.Replace($"({portName})", "").Trim();
                    descriptions[portName] = description;
                }
            }
        }
        catch
        {
            // WMI 查询可能失败，忽略错误
        }

        return descriptions;
    }
}
