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
    private readonly SerialCommunicationViewModel _viewModel;

    public SerialCommunicationViewModelTests()
    {
        _mockSerialPortService = new Mock<ISerialPortService>();
        _viewModel = new SerialCommunicationViewModel(_mockSerialPortService.Object);
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
}
