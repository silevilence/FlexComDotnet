using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Serial.ViewModels;

/// <summary>
/// 串口收发通信 ViewModel
/// </summary>
public partial class SerialCommunicationViewModel : ObservableObject
{
    private readonly ISerialPortService _serialPortService;
    private readonly StringBuilder _receivedBuffer = new();

    /// <summary>
    /// 接收到的数据
    /// </summary>
    [ObservableProperty]
    private string _receivedData = string.Empty;

    /// <summary>
    /// 待发送的文本
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _sendText = string.Empty;

    /// <summary>
    /// 是否使用 Hex 显示模式（接收区）
    /// </summary>
    [ObservableProperty]
    private bool _isHexDisplayMode;

    /// <summary>
    /// 是否使用 Hex 发送模式
    /// </summary>
    [ObservableProperty]
    private bool _isHexSendMode;

    /// <summary>
    /// 发送状态信息
    /// </summary>
    [ObservableProperty]
    private string _sendStatus = string.Empty;

    /// <summary>
    /// 是否已连接（用于 UI 绑定）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isConnected;

    public SerialCommunicationViewModel(ISerialPortService serialPortService)
    {
        _serialPortService = serialPortService;

        // 订阅数据接收事件
        _serialPortService.DataReceived += OnDataReceived;
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;

        // 初始化连接状态
        IsConnected = _serialPortService.IsConnected;
    }

    /// <summary>
    /// 发送数据命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        if (string.IsNullOrEmpty(SendText))
        {
            return;
        }

        byte[] data;
        if (IsHexSendMode)
        {
            // Hex 模式发送
            if (!HexHelper.IsValidHexString(SendText))
            {
                SendStatus = "发送失败: 无效的十六进制格式";
                return;
            }
            data = HexHelper.HexStringToBytes(SendText);
            if (data.Length == 0 && !string.IsNullOrWhiteSpace(SendText))
            {
                SendStatus = "发送失败: 无效的十六进制格式";
                return;
            }
        }
        else
        {
            // ASCII 模式发送
            data = HexHelper.AsciiStringToBytes(SendText);
        }

        if (data.Length == 0)
        {
            return;
        }

        var success = _serialPortService.Send(data);
        if (success)
        {
            // 将发送的数据显示到接收区
            AppendToDisplay(data, isTx: true);
            SendStatus = $"发送成功: {data.Length} 字节";
        }
        else
        {
            SendStatus = "发送失败";
        }
    }

    private bool CanSend() => IsConnected && !string.IsNullOrEmpty(SendText);

    /// <summary>
    /// 清空接收区命令
    /// </summary>
    [RelayCommand]
    private void ClearReceived()
    {
        _receivedBuffer.Clear();
        ReceivedData = string.Empty;
    }

    /// <summary>
    /// 清空发送区命令
    /// </summary>
    [RelayCommand]
    private void ClearSend()
    {
        SendText = string.Empty;
        SendStatus = string.Empty;
    }

    /// <summary>
    /// 将数据追加到显示区域
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="isTx">是否为发送数据</param>
    private void AppendToDisplay(byte[] data, bool isTx)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        var prefix = isTx ? "[TX] " : "[RX] ";
        string displayText;

        if (IsHexDisplayMode)
        {
            displayText = prefix + HexHelper.BytesToHexString(data);
        }
        else
        {
            displayText = prefix + HexHelper.BytesToAsciiString(data, '.');
        }

        _receivedBuffer.AppendLine(displayText);
        ReceivedData = _receivedBuffer.ToString();
    }

    /// <summary>
    /// 数据接收处理
    /// </summary>
    private void OnDataReceived(object? sender, byte[] data)
    {
        AppendToDisplay(data, isTx: false);
    }

    /// <summary>
    /// 连接状态变化处理
    /// </summary>
    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
    }
}
