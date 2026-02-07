using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Scripting;

/// <summary>
/// 脚本 API 桥接测试
/// </summary>
public class ScriptApiBridgeTests
{
    private readonly Mock<ISerialPortService> _mockSerialService;
    private readonly Mock<IChecksumService> _mockChecksumService;
    private readonly ScriptApiBridge _bridge;

    public ScriptApiBridgeTests()
    {
        _mockSerialService = new Mock<ISerialPortService>();
        _mockChecksumService = new Mock<IChecksumService>();
        _bridge = new ScriptApiBridge(_mockSerialService.Object, _mockChecksumService.Object);
        _bridge.SetScriptName("test_script");
    }

    #region Send 测试

    [Fact]
    public void Send_ValidHex_ShouldSendBytes()
    {
        _mockSerialService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);

        var result = _bridge.Send("FF 01 02");

        result.Should().BeTrue();
        _mockSerialService.Verify(s => s.Send(
            It.Is<byte[]>(b => b.Length == 3 && b[0] == 0xFF && b[1] == 0x01 && b[2] == 0x02)),
            Times.Once);
    }

    [Fact]
    public void Send_InvalidHex_ShouldReturnFalse()
    {
        var result = _bridge.Send("ZZ GG");

        result.Should().BeFalse();
    }

    [Fact]
    public void Send_EmptyString_ShouldReturnFalse()
    {
        var result = _bridge.Send("");

        result.Should().BeFalse();
    }

    [Fact]
    public void SendBytes_ShouldDelegateToSerialService()
    {
        _mockSerialService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _bridge.SendBytes(data);

        result.Should().BeTrue();
        _mockSerialService.Verify(s => s.Send(data), Times.Once);
    }

    [Fact]
    public void SendText_ShouldDelegateToSerialService()
    {
        _mockSerialService.Setup(s => s.Send(It.IsAny<string>())).Returns(true);

        var result = _bridge.SendText("Hello");

        result.Should().BeTrue();
        _mockSerialService.Verify(s => s.Send("Hello"), Times.Once);
    }

    [Fact]
    public void Send_WhenServiceThrows_ShouldReturnFalse()
    {
        _mockSerialService.Setup(s => s.Send(It.IsAny<byte[]>()))
            .Throws(new InvalidOperationException("Port closed"));

        var result = _bridge.Send("FF 01");

        result.Should().BeFalse();
    }

    #endregion

    #region Log 测试

    [Fact]
    public void Log_ShouldRaiseLogOutputEvent()
    {
        ScriptLogEntry? capturedEntry = null;
        _bridge.LogOutput += (_, entry) => capturedEntry = entry;

        _bridge.Log("hello world");

        capturedEntry.Should().NotBeNull();
        capturedEntry!.Message.Should().Be("hello world");
        capturedEntry.Level.Should().Be(ScriptLogLevel.Info);
        capturedEntry.ScriptName.Should().Be("test_script");
    }

    [Fact]
    public void LogDebug_ShouldRaiseDebugLevelLog()
    {
        ScriptLogEntry? capturedEntry = null;
        _bridge.LogOutput += (_, entry) => capturedEntry = entry;

        _bridge.LogDebug("debug message");

        capturedEntry.Should().NotBeNull();
        capturedEntry!.Level.Should().Be(ScriptLogLevel.Debug);
    }

    [Fact]
    public void LogWarning_ShouldRaiseWarningLevelLog()
    {
        ScriptLogEntry? capturedEntry = null;
        _bridge.LogOutput += (_, entry) => capturedEntry = entry;

        _bridge.LogWarning("warning message");

        capturedEntry.Should().NotBeNull();
        capturedEntry!.Level.Should().Be(ScriptLogLevel.Warning);
    }

    [Fact]
    public void LogError_ShouldRaiseErrorLevelLog()
    {
        ScriptLogEntry? capturedEntry = null;
        _bridge.LogOutput += (_, entry) => capturedEntry = entry;

        _bridge.LogError("error message");

        capturedEntry.Should().NotBeNull();
        capturedEntry!.Level.Should().Be(ScriptLogLevel.Error);
    }

    #endregion

    #region Delay 测试

    [Fact]
    public void Delay_ShouldPauseExecution()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _bridge.Delay(50);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(30); // 容差
    }

    [Fact]
    public void Delay_WithCancellation_ShouldThrow()
    {
        var cts = new CancellationTokenSource();
        _bridge.SetCancellationToken(cts.Token);
        cts.Cancel();

        var action = () => _bridge.Delay(1000);
        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Delay_NegativeValue_ShouldNotThrow()
    {
        var action = () => _bridge.Delay(-1);
        action.Should().NotThrow();
    }

    #endregion

    #region Checksum 计算测试

    [Fact]
    public void Crc16_ShouldDelegateToChecksumService()
    {
        _mockChecksumService.Setup(s => s.CalculateAsHexString(
            ChecksumAlgorithmType.Crc16Modbus,
            It.IsAny<byte[]>()))
            .Returns("AB CD");

        var result = _bridge.Crc16("01 03 00 00 00 01");

        result.Should().Be("AB CD");
    }

    [Fact]
    public void Crc32_ShouldDelegateToChecksumService()
    {
        _mockChecksumService.Setup(s => s.CalculateAsHexString(
            ChecksumAlgorithmType.Crc32,
            It.IsAny<byte[]>()))
            .Returns("12 34 56 78");

        var result = _bridge.Crc32("01 03");

        result.Should().Be("12 34 56 78");
    }

    [Fact]
    public void Checksum_ShouldDelegateToChecksumService()
    {
        _mockChecksumService.Setup(s => s.CalculateAsHexString(
            ChecksumAlgorithmType.Sum8,
            It.IsAny<byte[]>()))
            .Returns("04");

        var result = _bridge.Checksum("01 03");

        result.Should().Be("04");
    }

    [Fact]
    public void Crc16_EmptyInput_ShouldReturnEmptyString()
    {
        var result = _bridge.Crc16("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Crc16_InvalidHex_ShouldReturnEmptyString()
    {
        var result = _bridge.Crc16("ZZ GG");

        result.Should().BeEmpty();
    }

    #endregion

    #region Timestamp 测试

    [Fact]
    public void GetTimestamp_ShouldReturnUnixMilliseconds()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = _bridge.GetTimestamp();
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        result.Should().BeGreaterThanOrEqualTo(before);
        result.Should().BeLessThanOrEqualTo(after);
    }

    #endregion

    #region Hex 转换测试

    [Fact]
    public void HexToBytes_ValidHex_ShouldConvert()
    {
        var result = _bridge.HexToBytes("FF 01 02");

        result.Should().Equal(0xFF, 0x01, 0x02);
    }

    [Fact]
    public void HexToBytes_EmptyString_ShouldReturnEmpty()
    {
        var result = _bridge.HexToBytes("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void BytesToHex_ShouldConvert()
    {
        var result = _bridge.BytesToHex([0xFF, 0x01, 0x02]);

        result.Should().Be("FF 01 02");
    }

    [Fact]
    public void BytesToHex_EmptyArray_ShouldReturnEmpty()
    {
        var result = _bridge.BytesToHex([]);

        result.Should().BeEmpty();
    }

    #endregion

    #region ScriptName 设置测试

    [Fact]
    public void SetScriptName_ShouldUpdateLogScriptName()
    {
        ScriptLogEntry? capturedEntry = null;
        _bridge.LogOutput += (_, entry) => capturedEntry = entry;

        _bridge.SetScriptName("new_script");
        _bridge.Log("test");

        capturedEntry!.ScriptName.Should().Be("new_script");
    }

    #endregion
}
