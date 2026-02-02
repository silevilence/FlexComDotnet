using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Serial.ViewModels;

/// <summary>
/// 串口配置 ViewModel
/// </summary>
public partial class SerialConfigViewModel : ObservableObject
{
    private readonly ISerialPortService _serialPortService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleConnectionCommand))]
    private SerialPortInfo? _selectedPort;

    [ObservableProperty]
    private BaudRate _selectedBaudRate = BaudRate.Baud115200;

    [ObservableProperty]
    private DataBitsOption _selectedDataBits = DataBitsOption.Eight;

    [ObservableProperty]
    private StopBitsOption _selectedStopBits = StopBitsOption.One;

    [ObservableProperty]
    private ParityOption _selectedParity = ParityOption.None;

    [ObservableProperty]
    private FlowControlOption _selectedFlowControl = FlowControlOption.None;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleConnectionCommand))]
    [NotifyPropertyChangedFor(nameof(ConnectionButtonText))]
    [NotifyPropertyChangedFor(nameof(IsConfigEnabled))]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "未连接";

    /// <summary>
    /// 可用串口列表
    /// </summary>
    public ObservableCollection<SerialPortInfo> AvailablePorts { get; } = [];

    /// <summary>
    /// 可用波特率列表
    /// </summary>
    public IReadOnlyList<BaudRate> AvailableBaudRates { get; } = Enum.GetValues<BaudRate>();

    /// <summary>
    /// 可用数据位列表
    /// </summary>
    public IReadOnlyList<DataBitsOption> AvailableDataBits { get; } = Enum.GetValues<DataBitsOption>();

    /// <summary>
    /// 可用停止位列表
    /// </summary>
    public IReadOnlyList<StopBitsOption> AvailableStopBits { get; } = Enum.GetValues<StopBitsOption>();

    /// <summary>
    /// 可用校验位列表
    /// </summary>
    public IReadOnlyList<ParityOption> AvailableParities { get; } = Enum.GetValues<ParityOption>();

    /// <summary>
    /// 可用流控选项
    /// </summary>
    public IReadOnlyList<FlowControlOption> AvailableFlowControls { get; } = Enum.GetValues<FlowControlOption>();

    /// <summary>
    /// 连接按钮文本
    /// </summary>
    public string ConnectionButtonText => IsConnected ? "断开连接" : "打开串口";

    /// <summary>
    /// 配置区域是否可编辑
    /// </summary>
    public bool IsConfigEnabled => !IsConnected;

    public SerialConfigViewModel(ISerialPortService serialPortService)
    {
        _serialPortService = serialPortService;
        
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;
        _serialPortService.ErrorOccurred += OnErrorOccurred;

        RefreshPorts();
    }

    /// <summary>
    /// 刷新串口列表
    /// </summary>
    [RelayCommand]
    private void RefreshPorts()
    {
        var currentSelection = SelectedPort?.PortName;
        
        AvailablePorts.Clear();
        foreach (var port in _serialPortService.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        // 尝试恢复之前的选择
        if (currentSelection != null)
        {
            SelectedPort = AvailablePorts.FirstOrDefault(p => p.PortName == currentSelection);
        }

        // 如果没有选中，默认选择第一个
        SelectedPort ??= AvailablePorts.FirstOrDefault();

        if (AvailablePorts.Count == 0)
        {
            StatusMessage = "未检测到串口设备";
        }
        else
        {
            StatusMessage = $"检测到 {AvailablePorts.Count} 个串口";
        }
    }

    /// <summary>
    /// 切换连接状态
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleConnection))]
    private void ToggleConnection()
    {
        if (IsConnected)
        {
            _serialPortService.Close();
        }
        else
        {
            OpenPort();
        }
    }

    private bool CanToggleConnection() => SelectedPort != null || IsConnected;

    private void OpenPort()
    {
        if (SelectedPort == null)
        {
            StatusMessage = "请先选择串口";
            return;
        }

        var config = new SerialPortConfig
        {
            PortName = SelectedPort.PortName,
            BaudRate = SelectedBaudRate,
            DataBits = SelectedDataBits,
            StopBits = SelectedStopBits,
            Parity = SelectedParity,
            FlowControl = SelectedFlowControl
        };

        if (_serialPortService.Open(config))
        {
            StatusMessage = $"已连接到 {SelectedPort.PortName}";
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        if (!connected)
        {
            StatusMessage = "已断开连接";
        }
    }

    private void OnErrorOccurred(object? sender, string message)
    {
        StatusMessage = message;
    }
}
