using FluentAssertions;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// HexHelper 工具类测试
/// </summary>
public class HexHelperTests
{
    #region BytesToHexString Tests

    [Fact]
    public void BytesToHexString_WithValidBytes_ReturnsHexString()
    {
        // Arrange
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        var result = HexHelper.BytesToHexString(bytes);

        // Assert
        result.Should().Be("48 65 6C 6C 6F");
    }

    [Fact]
    public void BytesToHexString_WithEmptyBytes_ReturnsEmptyString()
    {
        // Arrange
        var bytes = Array.Empty<byte>();

        // Act
        var result = HexHelper.BytesToHexString(bytes);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BytesToHexString_WithSingleByte_ReturnsSingleHex()
    {
        // Arrange
        var bytes = new byte[] { 0xFF };

        // Act
        var result = HexHelper.BytesToHexString(bytes);

        // Assert
        result.Should().Be("FF");
    }

    [Fact]
    public void BytesToHexString_WithCustomSeparator_UsesCorrectSeparator()
    {
        // Arrange
        var bytes = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = HexHelper.BytesToHexString(bytes, "-");

        // Assert
        result.Should().Be("01-02-03");
    }

    [Fact]
    public void BytesToHexString_WithNoSeparator_ReturnsNoSpaces()
    {
        // Arrange
        var bytes = new byte[] { 0xAB, 0xCD };

        // Act
        var result = HexHelper.BytesToHexString(bytes, "");

        // Assert
        result.Should().Be("ABCD");
    }

    #endregion

    #region HexStringToBytes Tests

    [Fact]
    public void HexStringToBytes_WithValidHexWithSpaces_ReturnsBytes()
    {
        // Arrange
        var hexString = "48 65 6C 6C 6F";

        // Act
        var result = HexHelper.HexStringToBytes(hexString);

        // Assert
        result.Should().BeEquivalentTo(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F });
    }

    [Fact]
    public void HexStringToBytes_WithValidHexWithoutSpaces_ReturnsBytes()
    {
        // Arrange
        var hexString = "48656C6C6F";

        // Act
        var result = HexHelper.HexStringToBytes(hexString);

        // Assert
        result.Should().BeEquivalentTo(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F });
    }

    [Fact]
    public void HexStringToBytes_WithLowercaseHex_ReturnsBytes()
    {
        // Arrange
        var hexString = "ab cd ef";

        // Act
        var result = HexHelper.HexStringToBytes(hexString);

        // Assert
        result.Should().BeEquivalentTo(new byte[] { 0xAB, 0xCD, 0xEF });
    }

    [Fact]
    public void HexStringToBytes_WithEmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var hexString = "";

        // Act
        var result = HexHelper.HexStringToBytes(hexString);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HexStringToBytes_WithInvalidHex_ReturnsEmptyArray()
    {
        // Arrange
        var hexString = "GG HH";

        // Act
        var result = HexHelper.HexStringToBytes(hexString);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HexStringToBytes_WithOddLengthHex_HandlesProperly()
    {
        // Arrange
        var hexString = "ABC"; // 奇数长度，应处理为 0A BC

        // Act
        var result = HexHelper.HexStringToBytes(hexString);

        // Assert
        result.Should().BeEquivalentTo(new byte[] { 0x0A, 0xBC });
    }

    #endregion

    #region BytesToAsciiString Tests

    [Fact]
    public void BytesToAsciiString_WithPrintableChars_ReturnsString()
    {
        // Arrange
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        var result = HexHelper.BytesToAsciiString(bytes);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public void BytesToAsciiString_WithNonPrintableChars_ReplacesWithDot()
    {
        // Arrange
        var bytes = new byte[] { 0x48, 0x00, 0x69 }; // "H" + null + "i"

        // Act
        var result = HexHelper.BytesToAsciiString(bytes, replacementChar: '.');

        // Assert
        result.Should().Be("H.i");
    }

    [Fact]
    public void BytesToAsciiString_WithEmptyBytes_ReturnsEmptyString()
    {
        // Arrange
        var bytes = Array.Empty<byte>();

        // Act
        var result = HexHelper.BytesToAsciiString(bytes);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AsciiStringToBytes Tests

    [Fact]
    public void AsciiStringToBytes_WithValidString_ReturnsBytes()
    {
        // Arrange
        var text = "Hello";

        // Act
        var result = HexHelper.AsciiStringToBytes(text);

        // Assert
        result.Should().BeEquivalentTo(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F });
    }

    [Fact]
    public void AsciiStringToBytes_WithEmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var text = "";

        // Act
        var result = HexHelper.AsciiStringToBytes(text);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region IsValidHexString Tests

    [Theory]
    [InlineData("48 65 6C 6C 6F", true)]
    [InlineData("48656C6C6F", true)]
    [InlineData("AB CD EF", true)]
    [InlineData("abcdef", true)]
    [InlineData("", true)]
    [InlineData("GG HH", false)]
    [InlineData("12 34 GH", false)]
    [InlineData("Hello", false)]
    public void IsValidHexString_ReturnsExpectedResult(string input, bool expected)
    {
        // Act
        var result = HexHelper.IsValidHexString(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
