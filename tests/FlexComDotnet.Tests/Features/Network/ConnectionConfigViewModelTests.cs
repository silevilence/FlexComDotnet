using FlexComDotnet.Core.Features.Network.Models;
using FlexComDotnet.Core.Features.Network.Services;
using FlexComDotnet.Core.Features.Network.ViewModels;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Network;

public class ConnectionConfigViewModelTests
{
    private readonly Mock<ISerialPortService> _serialPortServiceMock;
    private readonly Mock<ITcpClientService> _tcpClientServiceMock;
    private readonly Mock<ITcpServerService> _tcpServerServiceMock;
    private readonly Mock<IUdpService> _udpServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;

    public ConnectionConfigViewModelTests()
    {
        _serialPortServiceMock = new Mock<ISerialPortService>();
        _tcpClientServiceMock = new Mock<ITcpClientService>();
        _tcpServerServiceMock = new Mock<ITcpServerService>();
        _udpServiceMock = new Mock<IUdpService>();
        _configurationServiceMock = new Mock<IConfigurationService>();

        // 默认返回空的端口列表
        _serialPortServiceMock.Setup(s => s.GetAvailablePorts())
            .Returns([]);

        // 默认返回空配置
        _configurationServiceMock.Setup(s => s.Load())
            .Returns(new AppConfig());
    }

    private ConnectionConfigViewModel CreateViewModel()
    {
        return new ConnectionConfigViewModel(
            _serialPortServiceMock.Object,
            _tcpClientServiceMock.Object,
            _tcpServerServiceMock.Object,
            _udpServiceMock.Object,
            _configurationServiceMock.Object);
    }

    #region 配置加载测试

    [Fact]
    public void Constructor_ShouldLoadSerialConfig()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            SerialConfig = new SerialPortConfig
            {
                BaudRate = BaudRate.Baud9600,
                DataBits = DataBitsOption.Seven,
                StopBits = StopBitsOption.Two,
                Parity = ParityOption.Even,
                FlowControl = FlowControlOption.RtsCts
            }
        };
        _configurationServiceMock.Setup(s => s.Load()).Returns(appConfig);

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.SelectedBaudRate.Should().Be(BaudRate.Baud9600);
        viewModel.SelectedDataBits.Should().Be(DataBitsOption.Seven);
        viewModel.SelectedStopBits.Should().Be(StopBitsOption.Two);
        viewModel.SelectedParity.Should().Be(ParityOption.Even);
        viewModel.SelectedFlowControl.Should().Be(FlowControlOption.RtsCts);
    }

    [Fact]
    public void Constructor_ShouldLoadConnectionType()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            ConnectionConfig = new ConnectionConfig
            {
                SelectedConnectionType = ConnectionType.TcpClient
            }
        };
        _configurationServiceMock.Setup(s => s.Load()).Returns(appConfig);

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.SelectedConnectionType.Should().Be(ConnectionType.TcpClient);
    }

    [Fact]
    public void Constructor_ShouldLoadTcpClientConfig()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            ConnectionConfig = new ConnectionConfig
            {
                TcpClientConfig = new TcpClientConfig
                {
                    Host = "192.168.1.100",
                    Port = 9999,
                    ConnectTimeout = 10000,
                    KeepAlive = false,
                    NoDelay = true
                }
            }
        };
        _configurationServiceMock.Setup(s => s.Load()).Returns(appConfig);

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.TcpClientHost.Should().Be("192.168.1.100");
        viewModel.TcpClientPort.Should().Be(9999);
        viewModel.TcpClientConnectTimeout.Should().Be(10000);
        viewModel.TcpClientKeepAlive.Should().BeFalse();
        viewModel.TcpClientNoDelay.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldLoadTcpServerConfig()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            ConnectionConfig = new ConnectionConfig
            {
                TcpServerConfig = new TcpServerConfig
                {
                    ListenAddress = "192.168.1.1",
                    Port = 8888,
                    MaxConnections = 50
                }
            }
        };
        _configurationServiceMock.Setup(s => s.Load()).Returns(appConfig);

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.TcpServerListenAddress.Should().Be("192.168.1.1");
        viewModel.TcpServerPort.Should().Be(8888);
        viewModel.TcpServerMaxConnections.Should().Be(50);
    }

    [Fact]
    public void Constructor_ShouldLoadUdpConfig()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            ConnectionConfig = new ConnectionConfig
            {
                UdpConfig = new UdpConfig
                {
                    LocalPort = 5000,
                    RemoteHost = "10.0.0.1",
                    RemotePort = 6000,
                    EnableBroadcast = true
                }
            }
        };
        _configurationServiceMock.Setup(s => s.Load()).Returns(appConfig);

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.UdpLocalPort.Should().Be(5000);
        viewModel.UdpRemoteHost.Should().Be("10.0.0.1");
        viewModel.UdpRemotePort.Should().Be(6000);
        viewModel.UdpEnableBroadcast.Should().BeTrue();
    }

    #endregion

    #region 配置保存测试

    [Fact]
    public void ChangingConnectionType_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.SelectedConnectionType = ConnectionType.TcpServer;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.SelectedConnectionType == ConnectionType.TcpServer)), Times.Once);
    }

    [Fact]
    public void ChangingTcpClientHost_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpClientHost = "10.10.10.10";

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpClientConfig.Host == "10.10.10.10")), Times.Once);
    }

    [Fact]
    public void ChangingTcpClientPort_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpClientPort = 1234;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpClientConfig.Port == 1234)), Times.Once);
    }

    [Fact]
    public void ChangingTcpClientConnectTimeout_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpClientConnectTimeout = 15000;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpClientConfig.ConnectTimeout == 15000)), Times.Once);
    }

    [Fact]
    public void ChangingTcpClientKeepAlive_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpClientKeepAlive = false;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpClientConfig.KeepAlive == false)), Times.Once);
    }

    [Fact]
    public void ChangingTcpClientNoDelay_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpClientNoDelay = true;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpClientConfig.NoDelay == true)), Times.Once);
    }

    [Fact]
    public void ChangingTcpServerListenAddress_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpServerListenAddress = "192.168.1.1";

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpServerConfig.ListenAddress == "192.168.1.1")), Times.Once);
    }

    [Fact]
    public void ChangingTcpServerPort_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpServerPort = 9999;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpServerConfig.Port == 9999)), Times.Once);
    }

    [Fact]
    public void ChangingTcpServerMaxConnections_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.TcpServerMaxConnections = 100;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.TcpServerConfig.MaxConnections == 100)), Times.Once);
    }

    [Fact]
    public void ChangingUdpLocalPort_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.UdpLocalPort = 5555;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.UdpConfig.LocalPort == 5555)), Times.Once);
    }

    [Fact]
    public void ChangingUdpRemoteHost_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.UdpRemoteHost = "192.168.0.100";

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.UdpConfig.RemoteHost == "192.168.0.100")), Times.Once);
    }

    [Fact]
    public void ChangingUdpRemotePort_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.UdpRemotePort = 7777;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.UdpConfig.RemotePort == 7777)), Times.Once);
    }

    [Fact]
    public void ChangingUdpEnableBroadcast_ShouldSaveConfig()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _configurationServiceMock.Invocations.Clear();

        // Act
        viewModel.UdpEnableBroadcast = true;

        // Assert
        _configurationServiceMock.Verify(s => s.Save(It.Is<AppConfig>(c =>
            c.ConnectionConfig.UdpConfig.EnableBroadcast == true)), Times.Once);
    }

    #endregion

    #region 模式切换测试

    [Theory]
    [InlineData(ConnectionType.Serial)]
    [InlineData(ConnectionType.TcpClient)]
    [InlineData(ConnectionType.TcpServer)]
    [InlineData(ConnectionType.Udp)]
    public void IsMode_ShouldReflectSelectedConnectionType(ConnectionType connectionType)
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SelectedConnectionType = connectionType;

        // Assert
        viewModel.IsSerialMode.Should().Be(connectionType == ConnectionType.Serial);
        viewModel.IsTcpClientMode.Should().Be(connectionType == ConnectionType.TcpClient);
        viewModel.IsTcpServerMode.Should().Be(connectionType == ConnectionType.TcpServer);
        viewModel.IsUdpMode.Should().Be(connectionType == ConnectionType.Udp);
    }

    [Theory]
    [InlineData(ConnectionType.Serial, false, "打开串口")]
    [InlineData(ConnectionType.Serial, true, "断开连接")]
    [InlineData(ConnectionType.TcpClient, false, "连接服务器")]
    [InlineData(ConnectionType.TcpClient, true, "断开连接")]
    [InlineData(ConnectionType.TcpServer, false, "开始监听")]
    [InlineData(ConnectionType.TcpServer, true, "停止监听")]
    [InlineData(ConnectionType.Udp, false, "绑定端口")]
    [InlineData(ConnectionType.Udp, true, "关闭端口")]
    public void ConnectionButtonText_ShouldReflectStateAndType(ConnectionType type, bool isConnected, string expectedText)
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SelectedConnectionType = type;

        // 使用反射设置私有属性 (在实际情况下，这会通过连接状态事件触发)
        var isConnectedProperty = typeof(ConnectionConfigViewModel).GetProperty("IsConnected");
        isConnectedProperty?.SetValue(viewModel, isConnected);

        // Assert
        viewModel.ConnectionButtonText.Should().Be(expectedText);
    }

    #endregion

    #region 默认值测试

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert - 默认连接类型
        viewModel.SelectedConnectionType.Should().Be(ConnectionType.Serial);
        viewModel.IsConnected.Should().BeFalse();

        // Assert - 默认串口配置
        viewModel.SelectedBaudRate.Should().Be(BaudRate.Baud115200);
        viewModel.SelectedDataBits.Should().Be(DataBitsOption.Eight);
        viewModel.SelectedStopBits.Should().Be(StopBitsOption.One);
        viewModel.SelectedParity.Should().Be(ParityOption.None);
        viewModel.SelectedFlowControl.Should().Be(FlowControlOption.None);

        // Assert - 默认 TCP 客户端配置
        viewModel.TcpClientHost.Should().Be("127.0.0.1");
        viewModel.TcpClientPort.Should().Be(8080);
        viewModel.TcpClientConnectTimeout.Should().Be(5000);
        viewModel.TcpClientKeepAlive.Should().BeTrue();
        viewModel.TcpClientNoDelay.Should().BeFalse();

        // Assert - 默认 TCP 服务器配置
        viewModel.TcpServerListenAddress.Should().Be("0.0.0.0");
        viewModel.TcpServerPort.Should().Be(8080);
        viewModel.TcpServerMaxConnections.Should().Be(10);

        // Assert - 默认 UDP 配置
        viewModel.UdpLocalPort.Should().Be(0);
        viewModel.UdpRemoteHost.Should().Be("127.0.0.1");
        viewModel.UdpRemotePort.Should().Be(8080);
        viewModel.UdpEnableBroadcast.Should().BeFalse();
    }

    #endregion

    #region 配置可用性测试

    [Fact]
    public void IsConfigEnabled_ShouldBeTrueWhenDisconnected()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.IsConfigEnabled.Should().BeTrue();
    }

    [Fact]
    public void AvailableConnectionTypes_ShouldContainAllTypes()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Assert
        viewModel.AvailableConnectionTypes.Should().Contain(ConnectionType.Serial);
        viewModel.AvailableConnectionTypes.Should().Contain(ConnectionType.TcpClient);
        viewModel.AvailableConnectionTypes.Should().Contain(ConnectionType.TcpServer);
        viewModel.AvailableConnectionTypes.Should().Contain(ConnectionType.Udp);
        viewModel.AvailableConnectionTypes.Should().HaveCount(4);
    }

    #endregion
}
