using FluentAssertions;
using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

namespace FlexComDotnet.Tests.Features.Checksum;

/// <summary>
/// 校验和算法测试
/// </summary>
public class ChecksumAlgorithmTests
{
    #region Sum8 测试

    [Fact]
    public void Sum8_WithEmptyArray_ShouldReturnZero()
    {
        // Arrange
        var algorithm = new Sum8Algorithm();

        // Act
        var result = algorithm.Calculate([]);

        // Assert
        result.Should().Equal([0x00]);
    }

    [Fact]
    public void Sum8_WithSingleByte_ShouldReturnSameByte()
    {
        // Arrange
        var algorithm = new Sum8Algorithm();

        // Act
        var result = algorithm.Calculate([0x5A]);

        // Assert
        result.Should().Equal([0x5A]);
    }

    [Fact]
    public void Sum8_WithMultipleBytes_ShouldReturnLow8Bits()
    {
        // Arrange - 0x48 + 0x65 + 0x6C + 0x6C + 0x6F = 0x1F4, 低8位 = 0xF4
        var algorithm = new Sum8Algorithm();
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

        // Act
        var result = algorithm.Calculate(data);

        // Assert
        result.Should().Equal([0xF4]);
    }

    [Fact]
    public void Sum8_Properties_ShouldBeCorrect()
    {
        var algorithm = new Sum8Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Sum8);
        algorithm.ResultLength.Should().Be(1);
        algorithm.DisplayName.Should().NotBeEmpty();
    }

    #endregion

    #region Sum16 测试

    [Fact]
    public void Sum16_WithEmptyArray_ShouldReturnZeros()
    {
        var algorithm = new Sum16Algorithm();

        var result = algorithm.Calculate([]);

        result.Should().Equal([0x00, 0x00]);
    }

    [Fact]
    public void Sum16_WithMultipleBytes_ShouldReturnBigEndian()
    {
        // Arrange - 0x48 + 0x65 + 0x6C + 0x6C + 0x6F = 0x01F4
        // 大端序: 0x01, 0xF4
        var algorithm = new Sum16Algorithm();
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

        var result = algorithm.Calculate(data);

        result.Should().Equal([0x01, 0xF4]);
    }

    [Fact]
    public void Sum16_Properties_ShouldBeCorrect()
    {
        var algorithm = new Sum16Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Sum16);
        algorithm.ResultLength.Should().Be(2);
    }

    #endregion

    #region XOR 测试

    [Fact]
    public void Xor_WithEmptyArray_ShouldReturnZero()
    {
        var algorithm = new XorAlgorithm();

        var result = algorithm.Calculate([]);

        result.Should().Equal([0x00]);
    }

    [Fact]
    public void Xor_WithSingleByte_ShouldReturnSameByte()
    {
        var algorithm = new XorAlgorithm();

        var result = algorithm.Calculate([0xAB]);

        result.Should().Equal([0xAB]);
    }

    [Fact]
    public void Xor_WithMultipleBytes_ShouldReturnXorResult()
    {
        // Arrange - 0x01 ^ 0x02 ^ 0x03 ^ 0x04 = 0x04
        var algorithm = new XorAlgorithm();
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var result = algorithm.Calculate(data);

        result.Should().Equal([0x04]);
    }

    [Fact]
    public void Xor_Properties_ShouldBeCorrect()
    {
        var algorithm = new XorAlgorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Xor);
        algorithm.ResultLength.Should().Be(1);
    }

    #endregion

    #region CRC-8 测试

    [Fact]
    public void Crc8_WithEmptyArray_ShouldReturnInitialValue()
    {
        var algorithm = new Crc8Algorithm();

        var result = algorithm.Calculate([]);

        result.Should().Equal([0x00]);
    }

    [Fact]
    public void Crc8_WithKnownData_ShouldReturnCorrectCrc()
    {
        // 使用在线 CRC 计算器验证的标准测试向量
        // "123456789" 的 CRC-8 (多项式 0x07) = 0xF4
        var algorithm = new Crc8Algorithm();
        var data = "123456789"u8.ToArray();

        var result = algorithm.Calculate(data);

        result.Should().Equal([0xF4]);
    }

    [Fact]
    public void Crc8_Properties_ShouldBeCorrect()
    {
        var algorithm = new Crc8Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Crc8);
        algorithm.ResultLength.Should().Be(1);
    }

    #endregion

    #region CRC-16/MODBUS 测试

    [Fact]
    public void Crc16Modbus_WithEmptyArray_ShouldReturnInitialValue()
    {
        var algorithm = new Crc16ModbusAlgorithm();

        var result = algorithm.Calculate([]);

        // 初始值 0xFFFF, 小端序
        result.Should().Equal([0xFF, 0xFF]);
    }

    [Fact]
    public void Crc16Modbus_WithKnownData_ShouldReturnCorrectCrc()
    {
        // 标准 MODBUS 测试向量
        // 地址01, 功能码03, 起始地址00 00, 寄存器数量00 0A
        // CRC = 0xCDC5, 小端序 = 0xC5, 0xCD
        var algorithm = new Crc16ModbusAlgorithm();
        var data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };

        var result = algorithm.Calculate(data);

        result.Should().Equal([0xC5, 0xCD]);
    }

    [Fact]
    public void Crc16Modbus_WithStandardTestVector_ShouldReturnCorrectCrc()
    {
        // "123456789" 的 CRC-16/MODBUS = 0x4B37
        var algorithm = new Crc16ModbusAlgorithm();
        var data = "123456789"u8.ToArray();

        var result = algorithm.Calculate(data);

        // 小端序: 0x37, 0x4B
        result.Should().Equal([0x37, 0x4B]);
    }

    [Fact]
    public void Crc16Modbus_Properties_ShouldBeCorrect()
    {
        var algorithm = new Crc16ModbusAlgorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Crc16Modbus);
        algorithm.ResultLength.Should().Be(2);
    }

    #endregion

    #region CRC-16/CCITT-FALSE 测试

    [Fact]
    public void Crc16CcittFalse_WithEmptyArray_ShouldReturnInitialValue()
    {
        var algorithm = new Crc16CcittFalseAlgorithm();

        var result = algorithm.Calculate([]);

        // 初始值 0xFFFF, 大端序
        result.Should().Equal([0xFF, 0xFF]);
    }

    [Fact]
    public void Crc16CcittFalse_WithStandardTestVector_ShouldReturnCorrectCrc()
    {
        // "123456789" 的 CRC-16/CCITT-FALSE = 0x29B1
        var algorithm = new Crc16CcittFalseAlgorithm();
        var data = "123456789"u8.ToArray();

        var result = algorithm.Calculate(data);

        // 大端序: 0x29, 0xB1
        result.Should().Equal([0x29, 0xB1]);
    }

    [Fact]
    public void Crc16CcittFalse_Properties_ShouldBeCorrect()
    {
        var algorithm = new Crc16CcittFalseAlgorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Crc16CcittFalse);
        algorithm.ResultLength.Should().Be(2);
    }

    #endregion

    #region CRC-16/XMODEM 测试

    [Fact]
    public void Crc16Xmodem_WithEmptyArray_ShouldReturnInitialValue()
    {
        var algorithm = new Crc16XmodemAlgorithm();

        var result = algorithm.Calculate([]);

        // 初始值 0x0000, 大端序
        result.Should().Equal([0x00, 0x00]);
    }

    [Fact]
    public void Crc16Xmodem_WithStandardTestVector_ShouldReturnCorrectCrc()
    {
        // "123456789" 的 CRC-16/XMODEM = 0x31C3
        var algorithm = new Crc16XmodemAlgorithm();
        var data = "123456789"u8.ToArray();

        var result = algorithm.Calculate(data);

        // 大端序: 0x31, 0xC3
        result.Should().Equal([0x31, 0xC3]);
    }

    [Fact]
    public void Crc16Xmodem_Properties_ShouldBeCorrect()
    {
        var algorithm = new Crc16XmodemAlgorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Crc16Xmodem);
        algorithm.ResultLength.Should().Be(2);
    }

    #endregion

    #region CRC-32 测试

    [Fact]
    public void Crc32_WithEmptyArray_ShouldReturnCorrectValue()
    {
        var algorithm = new Crc32Algorithm();

        var result = algorithm.Calculate([]);

        // 空数据的 CRC-32 = 0xFFFFFFFF ^ 0xFFFFFFFF = 0x00000000
        // 小端序
        result.Should().Equal([0x00, 0x00, 0x00, 0x00]);
    }

    [Fact]
    public void Crc32_WithStandardTestVector_ShouldReturnCorrectCrc()
    {
        // "123456789" 的 CRC-32 = 0xCBF43926
        var algorithm = new Crc32Algorithm();
        var data = "123456789"u8.ToArray();

        var result = algorithm.Calculate(data);

        // 小端序: 0x26, 0x39, 0xF4, 0xCB
        result.Should().Equal([0x26, 0x39, 0xF4, 0xCB]);
    }

    [Fact]
    public void Crc32_Properties_ShouldBeCorrect()
    {
        var algorithm = new Crc32Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Crc32);
        algorithm.ResultLength.Should().Be(4);
    }

    #endregion

    #region MD5 测试

    [Fact]
    public void Md5_WithEmptyArray_ShouldReturnCorrectHash()
    {
        // 空数据的 MD5 = D41D8CD98F00B204E9800998ECF8427E
        var algorithm = new Md5Algorithm();

        var result = algorithm.Calculate([]);

        result.Should().HaveCount(16);
        result[0].Should().Be(0xD4);
        result[1].Should().Be(0x1D);
    }

    [Fact]
    public void Md5_WithKnownData_ShouldReturnCorrectHash()
    {
        // "hello" 的 MD5 = 5D41402ABC4B2A76B9719D911017C592
        var algorithm = new Md5Algorithm();
        var data = "hello"u8.ToArray();

        var result = algorithm.Calculate(data);

        result.Should().HaveCount(16);
        result[0].Should().Be(0x5D);
        result[1].Should().Be(0x41);
    }

    [Fact]
    public void Md5_Properties_ShouldBeCorrect()
    {
        var algorithm = new Md5Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Md5);
        algorithm.ResultLength.Should().Be(16);
    }

    #endregion

    #region SHA-1 测试

    [Fact]
    public void Sha1_WithEmptyArray_ShouldReturnCorrectHash()
    {
        // 空数据的 SHA-1 = DA39A3EE5E6B4B0D3255BFEF95601890AFD80709
        var algorithm = new Sha1Algorithm();

        var result = algorithm.Calculate([]);

        result.Should().HaveCount(20);
        result[0].Should().Be(0xDA);
        result[1].Should().Be(0x39);
    }

    [Fact]
    public void Sha1_WithKnownData_ShouldReturnCorrectHash()
    {
        // "hello" 的 SHA-1 = AAF4C61DDCC5E8A2DABEDE0F3B482CD9AEA9434D
        var algorithm = new Sha1Algorithm();
        var data = "hello"u8.ToArray();

        var result = algorithm.Calculate(data);

        result.Should().HaveCount(20);
        result[0].Should().Be(0xAA);
        result[1].Should().Be(0xF4);
    }

    [Fact]
    public void Sha1_Properties_ShouldBeCorrect()
    {
        var algorithm = new Sha1Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Sha1);
        algorithm.ResultLength.Should().Be(20);
    }

    #endregion

    #region SHA-256 测试

    [Fact]
    public void Sha256_WithEmptyArray_ShouldReturnCorrectHash()
    {
        // 空数据的 SHA-256 开头为 E3B0C44298FC1C14...
        var algorithm = new Sha256Algorithm();

        var result = algorithm.Calculate([]);

        result.Should().HaveCount(32);
        result[0].Should().Be(0xE3);
        result[1].Should().Be(0xB0);
    }

    [Fact]
    public void Sha256_WithKnownData_ShouldReturnCorrectHash()
    {
        // "hello" 的 SHA-256 = 2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824
        var algorithm = new Sha256Algorithm();
        var data = "hello"u8.ToArray();

        var result = algorithm.Calculate(data);

        result.Should().HaveCount(32);
        result[0].Should().Be(0x2C);
        result[1].Should().Be(0xF2);
    }

    [Fact]
    public void Sha256_Properties_ShouldBeCorrect()
    {
        var algorithm = new Sha256Algorithm();

        algorithm.Type.Should().Be(ChecksumAlgorithmType.Sha256);
        algorithm.ResultLength.Should().Be(32);
    }

    #endregion

    #region CalculateAsHexString 测试

    [Fact]
    public void CalculateAsHexString_ShouldReturnFormattedString()
    {
        var algorithm = new Sum8Algorithm();
        var data = new byte[] { 0x01, 0x02 };

        var result = algorithm.CalculateAsHexString(data);

        result.Should().Be("03");
    }

    [Fact]
    public void CalculateAsHexString_WithMultiByteResult_ShouldUseSpaceSeparator()
    {
        var algorithm = new Sum16Algorithm();
        var data = new byte[] { 0x01, 0x02 };

        var result = algorithm.CalculateAsHexString(data);

        // 0x01 + 0x02 = 0x0003, 大端序: 00 03
        result.Should().Be("00 03");
    }

    #endregion
}
