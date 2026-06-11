using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Network.Models;
using FlexComDotnet.Core.Features.Network.Services;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Network.ViewModels;

/// <summary>
/// 统一连接配置 ViewModel，支持串口、TCP、UDP 多种连接类型
/// </summary>
public partial class ConnectionConfigViewModel : ObservableObject
{
    private readonly ISerialPortService _serialPortService;
    private readonly ITcpClientService _tcpClientService;
    private readonly ITcpServerService _tcpServerService;
    private readonly IUdpService _udpService;
    private readonly IConfigurationService _configurationService;
    private bool _isInitializing = true;

    #region 通用属性

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSerialMode))]
    [NotifyPropertyChangedFor(nameof(IsTcpClientMode))]
    [NotifyPropertyChangedFor(nameof(IsTcpServerMode))]
    [NotifyPropertyChangedFor(nameof(IsUdpMode))]
    [NotifyPropertyChangedFor(nameof(ConnectionButtonText))]
    private ConnectionType _selectedConnectionType = ConnectionType.Serial;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionButtonText))]
    [NotifyPropertyChangedFor(nameof(IsConfigEnabled))]
    [NotifyCanExecuteChangedFor(nameof(ToggleConnectionCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "未连接";

    /// <summary>
    /// 可用连接类型列表
    /// </summary>
    public IReadOnlyList<ConnectionType> AvailableConnectionTypes { get; } = Enum.GetValues<ConnectionType>();

    /// <summary>
    /// 配置是否可编辑
    /// </summary>
    public bool IsConfigEnabled => !IsConnected;

    /// <summary>
    /// 连接按钮文本
    /// </summary>
    public string ConnectionButtonText => SelectedConnectionType switch
    {
        ConnectionType.Serial => IsConnected ? "断开连接" : "打开串口",
        ConnectionType.TcpClient => IsConnected ? "断开连接" : "连接服务器",
        ConnectionType.TcpServer => IsConnected ? "停止监听" : "开始监听",
        ConnectionType.Udp => IsConnected ? "关闭端口" : "绑定端口",
        _ => IsConnected ? "断开" : "连接"
    };

    /// <summary>
    /// 是否为串口模式
    /// </summary>
    public bool IsSerialMode => SelectedConnectionType == ConnectionType.Serial;

    /// <summary>
    /// 是否为 TCP 客户端模式
    /// </summary>
    public bool IsTcpClientMode => SelectedConnectionType == ConnectionType.TcpClient;

    /// <summary>
    /// 是否为 TCP 服务器模式
    /// </summary>
    public bool IsTcpServerMode => SelectedConnectionType == ConnectionType.TcpServer;

    /// <summary>
    /// 是否为 UDP 模式
    /// </summary>
    public bool IsUdpMode => SelectedConnectionType == ConnectionType.Udp;

    #endregion

    #region 串口配置属性

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

    /// <summary>
    /// 帧间隔超时阈值（毫秒）
    /// </summary>
    [ObservableProperty]
    private int _frameIntervalMs = 10;

    /// <summary>
    /// 最大帧长度（字节）
    /// </summary>
    [ObservableProperty]
    private int _maxFrameBytes = 4096;

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

    private string? _savedPortName;

    #endregion

    #region TCP 客户端配置属性

    [ObservableProperty]
    private string _tcpClientHost = "127.0.0.1";

    [ObservableProperty]
    private int _tcpClientPort = 8080;

    [ObservableProperty]
    private int _tcpClientConnectTimeout = 5000;

    [ObservableProperty]
    private bool _tcpClientKeepAlive = true;

    [ObservableProperty]
    private bool _tcpClientNoDelay = false;

    #endregion

    #region TCP 服务器配置属性

    [ObservableProperty]
    private string _tcpServerListenAddress = "0.0.0.0";

    [ObservableProperty]
    private int _tcpServerPort = 8080;

    [ObservableProperty]
    private int _tcpServerMaxConnections = 10;

    /// <summary>
    /// 已连接的客户端数量
    /// </summary>
    [ObservableProperty]
    private int _connectedClientsCount;

    #endregion

    #region UDP 配置属性

    [ObservableProperty]
    private int _udpLocalPort = 0;

    [ObservableProperty]
    private string _udpRemoteHost = "127.0.0.1";

    [ObservableProperty]
    private int _udpRemotePort = 8080;

    [ObservableProperty]
    private bool _udpEnableBroadcast = false;

    /// <summary>
    /// 实际绑定的本地端口
    /// </summary>
    [ObservableProperty]
    private int _udpBoundPort;

    #endregion

    public ConnectionConfigViewModel(
        ISerialPortService serialPortService,
        ITcpClientService tcpClientService,
        ITcpServerService tcpServerService,
        IUdpService udpService,
        IConfigurationService configurationService)
    {
        _serialPortService = serialPortService;
        _tcpClientService = tcpClientService;
        _tcpServerService = tcpServerService;
        _udpService = udpService;
        _configurationService = configurationService;

        // 订阅事件
        _serialPortService.ConnectionStateChanged += OnSerialConnectionStateChanged;
        _serialPortService.ErrorOccurred += OnErrorOccurred;

        _tcpClientService.StateChanged += OnTcpClientStateChanged;
        _tcpClientService.ErrorOccurred += OnErrorOccurred;

        _tcpServerService.StateChanged += OnTcpServerStateChanged;
        _tcpServerService.ErrorOccurred += OnErrorOccurred;
        _tcpServerService.ClientConnected += OnTcpServerClientConnected;
        _tcpServerService.ClientDisconnected += OnTcpServerClientDisconnected;

        _udpService.StateChanged += OnUdpStateChanged;
        _udpService.ErrorOccurred += OnErrorOccurred;

        // 加载保存的配置
        LoadSavedConfig();

        // 刷新串口列表
        RefreshPorts();

        // 初始化完成
        _isInitializing = false;
    }

    private void LoadSavedConfig()
    {
        var config = _configurationService.Load();
        
        // 加载串口配置
        var serialConfig = config.SerialConfig;
        SelectedBaudRate = serialConfig.BaudRate;
        SelectedDataBits = serialConfig.DataBits;
        SelectedStopBits = serialConfig.StopBits;
        SelectedParity = serialConfig.Parity;
        SelectedFlowControl = serialConfig.FlowControl;
        FrameIntervalMs = serialConfig.FrameIntervalMs;
        MaxFrameBytes = serialConfig.MaxFrameBytes;
        _savedPortName = serialConfig.PortName;

        // 加载连接配置
        var connectionConfig = config.ConnectionConfig;
        SelectedConnectionType = connectionConfig.SelectedConnectionType;

        // 加载 TCP 客户端配置
        TcpClientHost = connectionConfig.TcpClientConfig.Host;
        TcpClientPort = connectionConfig.TcpClientConfig.Port;
        TcpClientConnectTimeout = connectionConfig.TcpClientConfig.ConnectTimeout;
        TcpClientKeepAlive = connectionConfig.TcpClientConfig.KeepAlive;
        TcpClientNoDelay = connectionConfig.TcpClientConfig.NoDelay;

        // 加载 TCP 服务器配置
        TcpServerListenAddress = connectionConfig.TcpServerConfig.ListenAddress;
        TcpServerPort = connectionConfig.TcpServerConfig.Port;
        TcpServerMaxConnections = connectionConfig.TcpServerConfig.MaxConnections;

        // 加载 UDP 配置
        UdpLocalPort = connectionConfig.UdpConfig.LocalPort;
        UdpRemoteHost = connectionConfig.UdpConfig.RemoteHost;
        UdpRemotePort = connectionConfig.UdpConfig.RemotePort;
        UdpEnableBroadcast = connectionConfig.UdpConfig.EnableBroadcast;
    }

    #region 命令

    /// <summary>
    /// 刷新串口列表
    /// </summary>
    [RelayCommand]
    private void RefreshPorts()
    {
        var currentSelection = SelectedPort?.PortName ?? _savedPortName;

        AvailablePorts.Clear();
        foreach (var port in _serialPortService.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        if (currentSelection != null)
        {
            SelectedPort = AvailablePorts.FirstOrDefault(p => p.PortName == currentSelection);
        }

        SelectedPort ??= AvailablePorts.FirstOrDefault();

        if (IsSerialMode)
        {
            StatusMessage = AvailablePorts.Count == 0
                ? "未检测到串口设备"
                : $"检测到 {AvailablePorts.Count} 个串口";
        }
    }

    /// <summary>
    /// 切换连接状态
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleConnection))]
    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    private bool CanToggleConnection()
    {
        return SelectedConnectionType switch
        {
            ConnectionType.Serial => SelectedPort != null || IsConnected,
            _ => true
        };
    }

    private async Task ConnectAsync()
    {
        switch (SelectedConnectionType)
        {
            case ConnectionType.Serial:
                OpenSerialPort();
                break;
            case ConnectionType.TcpClient:
                await ConnectTcpClientAsync();
                break;
            case ConnectionType.TcpServer:
                await StartTcpServerAsync();
                break;
            case ConnectionType.Udp:
                await BindUdpAsync();
                break;
        }
    }

    private async Task DisconnectAsync()
    {
        switch (SelectedConnectionType)
        {
            case ConnectionType.Serial:
                _serialPortService.Close();
                break;
            case ConnectionType.TcpClient:
                await _tcpClientService.CloseAsync();
                break;
            case ConnectionType.TcpServer:
                await _tcpServerService.StopAsync();
                break;
            case ConnectionType.Udp:
                await _udpService.CloseAsync();
                break;
        }
    }

    #endregion

    #region 连接实现

    private void OpenSerialPort()
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
            FlowControl = SelectedFlowControl,
            FrameIntervalMs = FrameIntervalMs,
            MaxFrameBytes = MaxFrameBytes
        };

        if (_serialPortService.Open(config))
        {
            StatusMessage = $"已连接到 {SelectedPort.PortName}";
            SaveSerialConfig(config);
        }
    }

    private async Task ConnectTcpClientAsync()
    {
        if (string.IsNullOrWhiteSpace(TcpClientHost))
        {
            StatusMessage = "请输入服务器地址";
            return;
        }

        StatusMessage = $"正在连接 {TcpClientHost}:{TcpClientPort}...";

        var config = new TcpClientConfig
        {
            Host = TcpClientHost,
            Port = TcpClientPort,
            ConnectTimeout = TcpClientConnectTimeout,
            KeepAlive = TcpClientKeepAlive,
            NoDelay = TcpClientNoDelay
        };

        var result = await _tcpClientService.ConnectAsync(config);
        if (result)
        {
            StatusMessage = $"已连接到 {TcpClientHost}:{TcpClientPort}";
        }
    }

    private async Task StartTcpServerAsync()
    {
        StatusMessage = $"正在启动服务器 {TcpServerListenAddress}:{TcpServerPort}...";

        var config = new TcpServerConfig
        {
            ListenAddress = TcpServerListenAddress,
            Port = TcpServerPort,
            MaxConnections = TcpServerMaxConnections
        };

        var result = await _tcpServerService.StartAsync(config);
        if (result)
        {
            StatusMessage = $"正在监听 {TcpServerListenAddress}:{TcpServerPort}";
            ConnectedClientsCount = 0;
        }
    }

    private async Task BindUdpAsync()
    {
        StatusMessage = UdpLocalPort == 0 ? "正在绑定 UDP 端口..." : $"正在绑定端口 {UdpLocalPort}...";

        var config = new UdpConfig
        {
            LocalPort = UdpLocalPort,
            RemoteHost = UdpRemoteHost,
            RemotePort = UdpRemotePort,
            EnableBroadcast = UdpEnableBroadcast
        };

        var result = await _udpService.BindAsync(config);
        if (result)
        {
            UdpBoundPort = _udpService.LocalPort;
            StatusMessage = $"已绑定到端口 {UdpBoundPort}";
        }
    }

    #endregion

    #region 事件处理

    private void OnSerialConnectionStateChanged(object? sender, bool connected)
    {
        if (SelectedConnectionType == ConnectionType.Serial)
        {
            IsConnected = connected;
            if (!connected)
            {
                StatusMessage = "已断开连接";
            }
        }
    }

    private void OnTcpClientStateChanged(object? sender, ConnectionState state)
    {
        if (SelectedConnectionType == ConnectionType.TcpClient)
        {
            IsConnected = state == ConnectionState.Connected;
            if (state == ConnectionState.Disconnected)
            {
                StatusMessage = "已断开连接";
            }
        }
    }

    private void OnTcpServerStateChanged(object? sender, ConnectionState state)
    {
        if (SelectedConnectionType == ConnectionType.TcpServer)
        {
            IsConnected = state == ConnectionState.Listening;
            if (state == ConnectionState.Disconnected)
            {
                StatusMessage = "服务器已停止";
                ConnectedClientsCount = 0;
            }
        }
    }

    private void OnTcpServerClientConnected(object? sender, ClientInfo client)
    {
        ConnectedClientsCount = _tcpServerService.ConnectedClients.Count;
        StatusMessage = $"监听中，已连接 {ConnectedClientsCount} 个客户端";
    }

    private void OnTcpServerClientDisconnected(object? sender, ClientInfo client)
    {
        ConnectedClientsCount = _tcpServerService.ConnectedClients.Count;
        StatusMessage = $"监听中，已连接 {ConnectedClientsCount} 个客户端";
    }

    private void OnUdpStateChanged(object? sender, ConnectionState state)
    {
        if (SelectedConnectionType == ConnectionType.Udp)
        {
            IsConnected = state == ConnectionState.Connected;
            if (state == ConnectionState.Disconnected)
            {
                StatusMessage = "端口已关闭";
                UdpBoundPort = 0;
            }
        }
    }

    private void OnErrorOccurred(object? sender, string message)
    {
        StatusMessage = message;
    }

    #endregion

    #region 配置保存

    private void SaveSerialConfig(SerialPortConfig serialConfig)
    {
        var appConfig = _configurationService.Load();
        appConfig.SerialConfig = serialConfig.Clone();
        _configurationService.Save(appConfig);
    }

    /// <summary>
    /// 保存所有连接配置
    /// </summary>
    private void SaveConnectionConfig()
    {
        if (_isInitializing)
            return;

        var appConfig = _configurationService.Load();

        // 保存连接类型
        appConfig.ConnectionConfig.SelectedConnectionType = SelectedConnectionType;

        // 保存 TCP 客户端配置
        appConfig.ConnectionConfig.TcpClientConfig = new TcpClientConfig
        {
            Host = TcpClientHost,
            Port = TcpClientPort,
            ConnectTimeout = TcpClientConnectTimeout,
            KeepAlive = TcpClientKeepAlive,
            NoDelay = TcpClientNoDelay
        };

        // 保存 TCP 服务器配置
        appConfig.ConnectionConfig.TcpServerConfig = new TcpServerConfig
        {
            ListenAddress = TcpServerListenAddress,
            Port = TcpServerPort,
            MaxConnections = TcpServerMaxConnections
        };

        // 保存 UDP 配置
        appConfig.ConnectionConfig.UdpConfig = new UdpConfig
        {
            LocalPort = UdpLocalPort,
            RemoteHost = UdpRemoteHost,
            RemotePort = UdpRemotePort,
            EnableBroadcast = UdpEnableBroadcast
        };

        _configurationService.Save(appConfig);
    }

    #endregion

    partial void OnSelectedConnectionTypeChanged(ConnectionType value)
    {
        // 切换连接类型时更新状态消息
        StatusMessage = value switch
        {
            ConnectionType.Serial => AvailablePorts.Count == 0 ? "未检测到串口设备" : $"检测到 {AvailablePorts.Count} 个串口",
            ConnectionType.TcpClient => "未连接",
            ConnectionType.TcpServer => "服务器未启动",
            ConnectionType.Udp => "端口未绑定",
            _ => "未连接"
        };

        // 通知命令可执行状态变化
        ToggleConnectionCommand.NotifyCanExecuteChanged();

        // 保存配置
        SaveConnectionConfig();
    }

    #region TCP 客户端配置变更处理

    partial void OnTcpClientHostChanged(string value) => SaveConnectionConfig();
    partial void OnTcpClientPortChanged(int value) => SaveConnectionConfig();
    partial void OnTcpClientConnectTimeoutChanged(int value) => SaveConnectionConfig();
    partial void OnTcpClientKeepAliveChanged(bool value) => SaveConnectionConfig();
    partial void OnTcpClientNoDelayChanged(bool value) => SaveConnectionConfig();

    #endregion

    #region TCP 服务器配置变更处理

    partial void OnTcpServerListenAddressChanged(string value) => SaveConnectionConfig();
    partial void OnTcpServerPortChanged(int value) => SaveConnectionConfig();
    partial void OnTcpServerMaxConnectionsChanged(int value) => SaveConnectionConfig();

    #endregion

    #region UDP 配置变更处理

    partial void OnUdpLocalPortChanged(int value) => SaveConnectionConfig();
    partial void OnUdpRemoteHostChanged(string value) => SaveConnectionConfig();
    partial void OnUdpRemotePortChanged(int value) => SaveConnectionConfig();
    partial void OnUdpEnableBroadcastChanged(bool value) => SaveConnectionConfig();

    #endregion
}
