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
    private SerialPort? _serialPort;
    private bool _disposed;

    /// <inheritdoc/>
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    /// <inheritdoc/>
    public SerialPortConfig? CurrentConfig { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<byte[]>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<string>? ErrorOccurred;

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
            _serialPort.Write(data, 0, data.Length);
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
                DataReceived?.Invoke(this, buffer);
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
