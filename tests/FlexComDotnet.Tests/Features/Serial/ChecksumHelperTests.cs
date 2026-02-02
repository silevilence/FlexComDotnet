using FluentAssertions;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// ChecksumHelper 测试
/// </summary>
public class ChecksumHelperTests
{
    #region Sum8 测试

    [Fact]
    public void CalculateSum8_WithEmptyArray_ShouldReturnZero()
    {
        // Act
        var result = ChecksumHelper.CalculateSum8([]);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateSum8_WithSingleByte_ShouldReturnSameByte()
    {
        // Act
        var result = ChecksumHelper.CalculateSum8([0x5A]);

        // Assert
        result.Should().Be(0x5A);
    }

    [Fact]
    public void CalculateSum8_WithMultipleBytes_ShouldReturnSum()
    {
        // Arrange - "Hello" = 0x48 + 0x65 + 0x6C + 0x6C + 0x6F = 0x1F4, Sum8 = 0xF4
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

        // Act
        var result = ChecksumHelper.CalculateSum8(data);

        // Assert
        result.Should().Be(0xF4);
    }

    [Fact]
    public void CalculateSum8_ShouldOverflowCorrectly()
    {
        // Arrange - 0xFF + 0xFF = 0x1FE, Sum8 = 0xFE
        var data = new byte[] { 0xFF, 0xFF };

        // Act
        var result = ChecksumHelper.CalculateSum8(data);

        // Assert
        result.Should().Be(0xFE);
    }

    #endregion

    #region CRC16-MODBUS 测试

    [Fact]
    public void CalculateCrc16Modbus_WithEmptyArray_ShouldReturnInitialValue()
    {
        // Act
        var result = ChecksumHelper.CalculateCrc16Modbus([]);

        // Assert
        result.Should().Be(0xFFFF); // CRC-16 MODBUS 初始值
    }

    [Fact]
    public void CalculateCrc16Modbus_WithKnownData_ShouldReturnCorrectCrc()
    {
        // Arrange - 标准 MODBUS 测试向量
        // 地址01, 功能码03, 起始地址00 00, 寄存器数量00 0A
        var data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };

        // Act
        var result = ChecksumHelper.CalculateCrc16Modbus(data);

        // Assert - CRC 应该是 0xCDC5
        result.Should().Be(0xCDC5);
    }

    [Fact]
    public void CalculateCrc16Modbus_WithSingleByte_ShouldReturnCorrectCrc()
    {
        // Arrange
        var data = new byte[] { 0x01 };

        // Act
        var result = ChecksumHelper.CalculateCrc16Modbus(data);

        // Assert - 使用在线工具验证
        result.Should().Be(0x807E);
    }

    #endregion

    #region AppendChecksum 测试

    [Fact]
    public void AppendChecksum_WithSum8_ShouldAppendOneByte()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

        // Act
        var result = ChecksumHelper.AppendChecksum(data, ChecksumType.Sum8);

        // Assert
        result.Should().HaveCount(6);
        result[5].Should().Be(0xF4);
    }

    [Fact]
    public void AppendChecksum_WithCrc16Modbus_ShouldAppendTwoBytes()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };

        // Act
        var result = ChecksumHelper.AppendChecksum(data, ChecksumType.Crc16Modbus);

        // Assert
        result.Should().HaveCount(8);
        // CRC16 MODBUS: 0xCDC5, 小端序 = C5 CD
        result[6].Should().Be(0xC5);
        result[7].Should().Be(0xCD);
    }

    [Fact]
    public void AppendChecksum_WithNone_ShouldReturnOriginalData()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

        // Act
        var result = ChecksumHelper.AppendChecksum(data, ChecksumType.None);

        // Assert
        result.Should().BeEquivalentTo(data);
    }

    #endregion
}
