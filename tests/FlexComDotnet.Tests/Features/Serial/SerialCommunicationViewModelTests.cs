using FluentAssertions;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using Moq;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// SerialCommunicationViewModel 测试
/// </summary>
public class SerialCommunicationViewModelTests
{
    private readonly Mock<ISerialPortService> _mockSerialPortService;
    private readonly Mock<IConfigurationService> _mockConfigurationService;
    private readonly SerialCommunicationViewModel _viewModel;

    public SerialCommunicationViewModelTests()
    {
        _mockSerialPortService = new Mock<ISerialPortService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        
        // 设置默认配置
        _mockConfigurationService.Setup(c => c.Load()).Returns(new AppConfig());
        
        _viewModel = new SerialCommunicationViewModel(_mockSerialPortService.Object, _mockConfigurationService.Object);
    }

    #region 初始化测试

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Assert
        _viewModel.ReceivedData.Should().BeEmpty();
        _viewModel.SendText.Should().BeEmpty();
        _viewModel.IsHexDisplayMode.Should().BeFalse();
        _viewModel.IsHexSendMode.Should().BeFalse();
    }

    #endregion

    #region 发送功能测试

    [Fact]
    public void SendCommand_WhenNotConnected_CanExecuteShouldBeFalse()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(false);
        _viewModel.SendText = "Hello";

        // Assert
        _viewModel.SendCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SendCommand_WhenConnectedWithAsciiMode_ShouldSendAsciiData()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        
        // 触发连接状态变化以更新 ViewModel 的 IsConnected
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "Hello";
        _viewModel.IsHexSendMode = false;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _mockSerialPortService.Verify(s => s.Send(It.Is<byte[]>(b => 
            b.SequenceEqual(HexHelper.AsciiStringToBytes("Hello")))), Times.Once);
    }

    [Fact]
    public void SendCommand_WhenConnectedWithHexMode_ShouldSendHexData()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        
        // 触发连接状态变化以更新 ViewModel 的 IsConnected
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "48 65 6C 6C 6F"; // "Hello" in hex
        _viewModel.IsHexSendMode = true;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _mockSerialPortService.Verify(s => s.Send(It.Is<byte[]>(b => 
            b.SequenceEqual(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }))), Times.Once);
    }

    [Fact]
    public void SendCommand_WhenSendTextIsEmpty_CanExecuteShouldBeFalse()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "";

        // Assert
        _viewModel.SendCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SendCommand_WithInvalidHex_ShouldNotSend()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "GG HH"; // 无效的 hex
        _viewModel.IsHexSendMode = true;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _mockSerialPortService.Verify(s => s.Send(It.IsAny<byte[]>()), Times.Never);
    }

    #endregion

    #region 接收功能测试

    [Fact]
    public void OnDataReceived_WithAsciiDisplayMode_ShouldAppendAsciiDataWithRxPrefix()
    {
        // Arrange
        _viewModel.IsHexDisplayMode = false;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);

        // Assert
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
    }

    [Fact]
    public void OnDataReceived_WithHexDisplayMode_ShouldAppendHexDataWithRxPrefix()
    {
        // Arrange
        _viewModel.IsHexDisplayMode = true;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);

        // Assert
        _viewModel.ReceivedData.Should().Contain("[RX] 48 65 6C 6C 6F");
    }

    [Fact]
    public void ToggleHexDisplayMode_ShouldRefreshDisplayWithNewFormat()
    {
        // Arrange - 先以 ASCII 模式接收数据
        _viewModel.IsHexDisplayMode = false;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
        _viewModel.ReceivedData.Should().NotContain("48 65 6C 6C 6F");

        // Act - 切换到 HEX 模式
        _viewModel.IsHexDisplayMode = true;

        // Assert - 已有数据应转换为 HEX 格式
        _viewModel.ReceivedData.Should().Contain("[RX] 48 65 6C 6C 6F");
        _viewModel.ReceivedData.Should().NotContain("[RX] Hello");
    }

    [Fact]
    public void ToggleHexDisplayMode_FromHexToAscii_ShouldRefreshDisplay()
    {
        // Arrange - 先以 HEX 模式接收数据
        _viewModel.IsHexDisplayMode = true;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);
        _viewModel.ReceivedData.Should().Contain("[RX] 48 65 6C 6C 6F");

        // Act - 切换到 ASCII 模式
        _viewModel.IsHexDisplayMode = false;

        // Assert - 已有数据应转换为 ASCII 格式
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
    }

    [Fact]
    public void SendCommand_WhenSuccess_ShouldAppendTxDataToDisplay()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "Hello";
        _viewModel.IsHexDisplayMode = false;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _viewModel.ReceivedData.Should().Contain("[TX] Hello");
    }

    [Fact]
    public void SendCommand_WhenSuccessInHexMode_ShouldAppendTxHexDataToDisplay()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "48 65"; // Hex for "He"
        _viewModel.IsHexSendMode = true;
        _viewModel.IsHexDisplayMode = true;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _viewModel.ReceivedData.Should().Contain("[TX] 48 65");
    }

    #endregion

    #region 清空功能测试

    [Fact]
    public void ClearReceivedCommand_ShouldClearReceivedData()
    {
        // Arrange
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);
        _viewModel.ReceivedData.Should().NotBeEmpty();

        // Act
        _viewModel.ClearReceivedCommand.Execute(null);

        // Assert
        _viewModel.ReceivedData.Should().BeEmpty();
    }

    [Fact]
    public void ClearSendCommand_ShouldClearSendText()
    {
        // Arrange
        _viewModel.SendText = "Hello";

        // Act
        _viewModel.ClearSendCommand.Execute(null);

        // Assert
        _viewModel.SendText.Should().BeEmpty();
    }

    #endregion

    #region 发送状态反馈测试

    [Fact]
    public void SendStatus_WhenSendSuccess_ShouldShowSuccess()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "Hello";

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _viewModel.SendStatus.Should().Contain("成功");
    }

    [Fact]
    public void SendStatus_WhenSendFail_ShouldShowFail()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(false);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "Hello";

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _viewModel.SendStatus.Should().Contain("失败");
    }

    #endregion

    #region CanSend 测试

    [Fact]
    public void CanSend_WhenConnectedAndHasText_ShouldReturnTrue()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        
        // 通过触发连接状态变化事件来更新 IsConnected
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "Hello";

        // Assert
        _viewModel.SendCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void CanSend_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(false);
        _viewModel.SendText = "Hello";

        // Assert
        _viewModel.SendCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region 数据统计测试

    [Fact]
    public void Constructor_ShouldInitializeCountersToZero()
    {
        // Assert
        _viewModel.RxBytes.Should().Be(0);
        _viewModel.TxBytes.Should().Be(0);
    }

    [Fact]
    public void OnDataReceived_ShouldIncrementRxBytes()
    {
        // Arrange
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // 5 bytes

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);

        // Assert
        _viewModel.RxBytes.Should().Be(5);
    }

    [Fact]
    public void OnDataReceived_ShouldAccumulateRxBytes()
    {
        // Arrange
        var testData1 = new byte[] { 0x48, 0x65 }; // 2 bytes
        var testData2 = new byte[] { 0x6C, 0x6C, 0x6F }; // 3 bytes

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData1);
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData2);

        // Assert
        _viewModel.RxBytes.Should().Be(5);
    }

    [Fact]
    public void SendCommand_WhenSuccess_ShouldIncrementTxBytes()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "Hello"; // 5 bytes

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _viewModel.TxBytes.Should().Be(5);
    }

    [Fact]
    public void SendCommand_WhenFail_ShouldNotIncrementTxBytes()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(false);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "Hello";

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert
        _viewModel.TxBytes.Should().Be(0);
    }

    [Fact]
    public void ResetCountersCommand_ShouldResetBothCountersToZero()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        // 发送一些数据
        _viewModel.SendText = "Hello";
        _viewModel.SendCommand.Execute(null);
        
        // 接收一些数据
        var testData = new byte[] { 0x48, 0x65, 0x6C };
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);
        
        _viewModel.RxBytes.Should().Be(3);
        _viewModel.TxBytes.Should().Be(5);

        // Act
        _viewModel.ResetCountersCommand.Execute(null);

        // Assert
        _viewModel.RxBytes.Should().Be(0);
        _viewModel.TxBytes.Should().Be(0);
    }

    #endregion

    #region 时间戳功能测试

    [Fact]
    public void Constructor_ShouldInitializeShowTimestampToFalse()
    {
        // Assert
        _viewModel.ShowTimestamp.Should().BeFalse();
    }

    [Fact]
    public void OnDataReceived_WithShowTimestamp_ShouldIncludeTimestamp()
    {
        // Arrange
        _viewModel.ShowTimestamp = true;
        _viewModel.IsHexDisplayMode = false;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);

        // Assert - 时间戳格式 [HH:mm:ss.fff]
        _viewModel.ReceivedData.Should().MatchRegex(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]");
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
    }

    [Fact]
    public void OnDataReceived_WithoutShowTimestamp_ShouldNotIncludeTimestamp()
    {
        // Arrange
        _viewModel.ShowTimestamp = false;
        _viewModel.IsHexDisplayMode = false;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);

        // Assert
        _viewModel.ReceivedData.Should().NotMatchRegex(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]");
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
    }

    [Fact]
    public void ToggleShowTimestamp_ShouldRefreshDisplayWithTimestamp()
    {
        // Arrange - 先接收数据（无时间戳）
        _viewModel.ShowTimestamp = false;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);
        _viewModel.ReceivedData.Should().NotMatchRegex(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]");

        // Act - 切换显示时间戳
        _viewModel.ShowTimestamp = true;

        // Assert - 已有数据应显示时间戳
        _viewModel.ReceivedData.Should().MatchRegex(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]");
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
    }

    #endregion

    #region 自动换行功能测试

    [Fact]
    public void Constructor_ShouldInitializeAutoLineBreakToTrue()
    {
        // Assert
        _viewModel.AutoLineBreak.Should().BeTrue();
    }

    [Fact]
    public void OnDataReceived_WithAutoLineBreak_ShouldAppendNewLine()
    {
        // Arrange
        _viewModel.AutoLineBreak = true;
        var testData1 = new byte[] { 0x41 }; // "A"
        var testData2 = new byte[] { 0x42 }; // "B"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData1);
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData2);

        // Assert - 每条数据应在单独的行
        var lines = _viewModel.ReceivedData.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public void OnDataReceived_WithoutAutoLineBreak_ShouldNotAppendNewLine()
    {
        // Arrange
        _viewModel.AutoLineBreak = false;
        var testData1 = new byte[] { 0x41 }; // "A"
        var testData2 = new byte[] { 0x42 }; // "B"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData1);
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData2);

        // Assert - 数据应在同一行（没有强制换行）
        var lines = _viewModel.ReceivedData.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1);
    }

    #endregion

    #region 暂停滚动功能测试

    [Fact]
    public void Constructor_ShouldInitializeIsPausedToFalse()
    {
        // Assert
        _viewModel.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void TogglePauseCommand_ShouldToggleIsPaused()
    {
        // Arrange
        _viewModel.IsPaused.Should().BeFalse();

        // Act
        _viewModel.TogglePauseCommand.Execute(null);

        // Assert
        _viewModel.IsPaused.Should().BeTrue();

        // Act again
        _viewModel.TogglePauseCommand.Execute(null);

        // Assert
        _viewModel.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void OnDataReceived_WhenPaused_ShouldNotUpdateDisplayButStillBuffer()
    {
        // Arrange
        _viewModel.IsPaused = true;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);

        // Assert - 显示区不应更新
        _viewModel.ReceivedData.Should().BeEmpty();
        
        // 但计数器应该更新
        _viewModel.RxBytes.Should().Be(5);
    }

    [Fact]
    public void OnDataReceived_WhenResumed_ShouldShowBufferedData()
    {
        // Arrange
        _viewModel.IsPaused = true;
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        _mockSerialPortService.Raise(s => s.DataReceived += null, _mockSerialPortService.Object, testData);
        _viewModel.ReceivedData.Should().BeEmpty();

        // Act - 恢复显示
        _viewModel.IsPaused = false;

        // Assert - 应该显示缓冲的数据
        _viewModel.ReceivedData.Should().Contain("[RX] Hello");
    }

    #endregion

    #region 发送辅助功能测试

    [Fact]
    public void Constructor_ShouldInitializeAppendCrLfToFalse()
    {
        // Assert
        _viewModel.AppendCrLf.Should().BeFalse();
    }

    [Fact]
    public void SendCommand_WithAppendCrLf_ShouldAppendCrLfToData()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "Hello";
        _viewModel.AppendCrLf = true;
        _viewModel.IsHexSendMode = false;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert - 应该发送 "Hello\r\n"
        _mockSerialPortService.Verify(s => s.Send(It.Is<byte[]>(b => 
            b.SequenceEqual(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x0D, 0x0A }))), Times.Once);
    }

    [Fact]
    public void SendCommand_WithoutAppendCrLf_ShouldNotAppendCrLf()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        _viewModel.SendText = "Hello";
        _viewModel.AppendCrLf = false;
        _viewModel.IsHexSendMode = false;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert - 应该只发送 "Hello"
        _mockSerialPortService.Verify(s => s.Send(It.Is<byte[]>(b => 
            b.SequenceEqual(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }))), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldInitializeAppendChecksumTypeToNone()
    {
        // Assert
        _viewModel.AppendChecksumType.Should().Be(ChecksumType.None);
    }

    [Fact]
    public void SendCommand_WithChecksumSum8_ShouldAppendChecksum()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        // "Hello" = 0x48 + 0x65 + 0x6C + 0x6C + 0x6F = 0x1F4, Sum8 = 0xF4
        _viewModel.SendText = "48 65 6C 6C 6F";
        _viewModel.AppendChecksumType = ChecksumType.Sum8;
        _viewModel.IsHexSendMode = true;

        // Act
        _viewModel.SendCommand.Execute(null);

        // Assert - 应该发送原数据 + 校验和
        _mockSerialPortService.Verify(s => s.Send(It.Is<byte[]>(b => 
            b.Length == 6 && b[5] == 0xF4)), Times.Once);
    }

    #endregion

    #region 定时发送功能测试

    [Fact]
    public void Constructor_ShouldInitializeTimerSettingsWithDefaults()
    {
        // Assert
        _viewModel.IsTimerEnabled.Should().BeFalse();
        _viewModel.TimerInterval.Should().Be(1000); // 默认1秒
    }

    [Fact]
    public void ToggleTimerCommand_WhenNotConnected_ShouldNotEnableTimer()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(false);
        _viewModel.SendText = "Hello";

        // Act
        _viewModel.ToggleTimerCommand.Execute(null);

        // Assert
        _viewModel.IsTimerEnabled.Should().BeFalse();
    }

    [Fact]
    public void ToggleTimerCommand_WhenConnectedWithText_ShouldToggleTimer()
    {
        // Arrange
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        _viewModel.SendText = "Hello";

        // Act
        _viewModel.ToggleTimerCommand.Execute(null);

        // Assert
        _viewModel.IsTimerEnabled.Should().BeTrue();
    }

    [Fact]
    public void TimerInterval_ShouldHaveMinimumValue()
    {
        // Arrange & Act
        _viewModel.TimerInterval = 5; // 太小的值

        // Assert - 应该被限制到最小值
        _viewModel.TimerInterval.Should().BeGreaterThanOrEqualTo(10);
    }

    #endregion

    #region 配置保存和加载测试

    [Fact]
    public void Constructor_ShouldLoadDisplayConfigFromConfiguration()
    {
        // Arrange
        var mockConfig = new Mock<IConfigurationService>();
        var appConfig = new AppConfig
        {
            DisplayConfig = new DisplayConfig
            {
                IsHexDisplayMode = true,
                ShowTimestamp = true,
                AutoLineBreak = false,
                IsHexSendMode = true
            }
        };
        mockConfig.Setup(c => c.Load()).Returns(appConfig);
        var mockSerialPort = new Mock<ISerialPortService>();

        // Act
        var viewModel = new SerialCommunicationViewModel(mockSerialPort.Object, mockConfig.Object);

        // Assert
        viewModel.IsHexDisplayMode.Should().BeTrue();
        viewModel.ShowTimestamp.Should().BeTrue();
        viewModel.AutoLineBreak.Should().BeFalse();
        viewModel.IsHexSendMode.Should().BeTrue();
    }

    [Fact]
    public void IsHexDisplayMode_WhenChanged_ShouldSaveConfig()
    {
        // Act
        _viewModel.IsHexDisplayMode = true;

        // Assert
        _mockConfigurationService.Verify(c => c.Save(It.Is<AppConfig>(cfg => 
            cfg.DisplayConfig.IsHexDisplayMode == true)), Times.Once);
    }

    [Fact]
    public void ShowTimestamp_WhenChanged_ShouldSaveConfig()
    {
        // Act
        _viewModel.ShowTimestamp = true;

        // Assert
        _mockConfigurationService.Verify(c => c.Save(It.Is<AppConfig>(cfg => 
            cfg.DisplayConfig.ShowTimestamp == true)), Times.Once);
    }

    [Fact]
    public void AutoLineBreak_WhenChanged_ShouldSaveConfig()
    {
        // Arrange - 默认是 true，所以改成 false 触发保存
        // 注意：构造函数已经将 AutoLineBreak 设为默认值 true

        // Act
        _viewModel.AutoLineBreak = false;

        // Assert
        _mockConfigurationService.Verify(c => c.Save(It.Is<AppConfig>(cfg => 
            cfg.DisplayConfig.AutoLineBreak == false)), Times.Once);
    }

    [Fact]
    public void IsHexSendMode_WhenChanged_ShouldSaveConfig()
    {
        // Act
        _viewModel.IsHexSendMode = true;

        // Assert
        _mockConfigurationService.Verify(c => c.Save(It.Is<AppConfig>(cfg => 
            cfg.DisplayConfig.IsHexSendMode == true)), Times.Once);
    }

    #endregion
}
