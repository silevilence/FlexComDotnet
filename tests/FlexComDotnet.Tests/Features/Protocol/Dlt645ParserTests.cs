using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Models.Dlt645;
using FlexComDotnet.Core.Features.Protocol.Services.Parsers;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Protocol;

public class Dlt645ParserTests
{
    private readonly Dlt645Parser _parser = new();

    private static byte CalculateChecksum(byte[] frame, int length)
    {
        byte sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += frame[i];
        }
        return sum;
    }

    private static byte[] BuildFrame(byte[] address, byte controlCode, byte[] data)
    {
        int frameLength = 12 + data.Length;
        var frame = new byte[frameLength];
        frame[0] = 0x68;
        Array.Copy(address, 0, frame, 1, 6);
        frame[7] = 0x68;
        frame[8] = controlCode;
        frame[9] = (byte)data.Length;
        Array.Copy(data, 0, frame, 10, data.Length);
        frame[frameLength - 2] = CalculateChecksum(frame, frameLength - 2);
        frame[frameLength - 1] = 0x16;
        return frame;
    }

    [Fact]
    public void Name_ShouldReturnCorrectProtocolName()
    {
        _parser.Name.Should().Be("DL/T 645-2007");
    }

    [Fact]
    public void Validate_WithValidFrame_ShouldReturnTrue()
    {
        byte[] address = [0x12, 0x34, 0x56, 0x78, 0x90, 0x12];
        byte[] data = [0x33, 0x33, 0x34, 0x33, 0x35, 0x33, 0x33, 0x33];
        var frame = BuildFrame(address, 0x91, data);

        _parser.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidStartByte_ShouldReturnFalse()
    {
        byte[] frame = [0x67, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x68, 0x91, 0x00, 0xC5, 0x16];

        _parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithInvalidSecondStartByte_ShouldReturnFalse()
    {
        byte[] frame = [0x68, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x67, 0x91, 0x00, 0xC5, 0x16];

        _parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithInvalidEndByte_ShouldReturnFalse()
    {
        byte[] frame = [0x68, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x68, 0x91, 0x00, 0xC5, 0x17];

        _parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTooShortFrame_ShouldReturnFalse()
    {
        byte[] frame = [0x68, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x68, 0x91, 0x00, 0x16];

        _parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithWrongDataLength_ShouldReturnFalse()
    {
        byte[] frame = [0x68, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x68, 0x91, 0x05, 0xC5, 0x16];

        _parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Parse_WithValidReadResponse_ShouldParseCorrectly()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [0x33, 0x33, 0x34, 0x33, 0x35, 0x33, 0x33, 0x33];
        var frame = BuildFrame(address, 0x91, data);

        var result = _parser.Parse(frame) as Dlt645ParsedFrame;

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.MeterAddress.Should().Be("999999999999");
        result.ControlCode.Should().NotBeNull();
        result.ControlCode!.IsResponse.Should().BeTrue();
        result.ControlCode.FunctionCode.Should().Be(Dlt645FunctionCode.ReadData);
        result.DataLength.Should().Be(8);
    }

    [Fact]
    public void Parse_WithErrorResponse_ShouldParseErrorCode()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [0x35];
        var frame = BuildFrame(address, 0xD1, data);

        var result = _parser.Parse(frame) as Dlt645ParsedFrame;

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.ControlCode!.IsError.Should().BeTrue();
        result.ErrorByte.Should().Be(0x02);
        result.ErrorDescriptions.Should().Contain("无请求数据");
    }

    [Fact]
    public void Parse_WithInvalidChecksum_ShouldReturnInvalid()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [];
        var frame = BuildFrame(address, 0x91, data);
        frame[^2] = 0x00;

        var result = _parser.Parse(frame);

        result.IsValid.Should().BeFalse();
        result.ChecksumValid.Should().BeFalse();
    }

    [Fact]
    public void TryExtractFrame_WithValidFrame_ShouldExtract()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [];
        var validFrame = BuildFrame(address, 0x91, data);
        byte[] buffer = [0xFE, 0xFE, .. validFrame, 0x00, 0x00];

        bool success = _parser.TryExtractFrame(buffer, out byte[] frame, out int consumed);

        success.Should().BeTrue();
        frame.Length.Should().Be(12);
        frame[0].Should().Be(0x68);
        consumed.Should().Be(14);
    }

    [Fact]
    public void TryExtractFrame_WithWakeupPreamble_ShouldSkipFE()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [];
        var validFrame = BuildFrame(address, 0x91, data);
        byte[] buffer = [0xFE, 0xFE, 0xFE, .. validFrame];

        bool success = _parser.TryExtractFrame(buffer, out byte[] frame, out int consumed);

        success.Should().BeTrue();
        frame[0].Should().Be(0x68);
        consumed.Should().Be(15);
    }

    [Fact]
    public void TryExtractFrame_WithIncompleteFrame_ShouldReturnFalse()
    {
        byte[] buffer = [0x68, 0x99, 0x99, 0x99, 0x99, 0x99, 0x99, 0x68, 0x91, 0x08];

        bool success = _parser.TryExtractFrame(buffer, out byte[] frame, out int consumed);

        success.Should().BeFalse();
        frame.Should().BeEmpty();
    }

    [Fact]
    public void TryExtractFrame_WithNoStartByte_ShouldConsumeBuffer()
    {
        byte[] buffer = [0x00, 0x01, 0x02, 0x03, 0x04];

        bool success = _parser.TryExtractFrame(buffer, out byte[] frame, out int consumed);

        success.Should().BeFalse();
        consumed.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Parse_ShouldDecodeDataFieldCorrectly()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [0x33, 0x33, 0x34, 0x33, 0x35, 0x33, 0x34, 0x35];
        var frame = BuildFrame(address, 0x91, data);

        var result = _parser.Parse(frame) as Dlt645ParsedFrame;

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.DecodedDataField.Should().NotBeEmpty();
        result.DecodedDataField[0].Should().Be(0x00);
        result.DecodedDataField[1].Should().Be(0x00);
        result.DecodedDataField[2].Should().Be(0x01);
        result.DecodedDataField[3].Should().Be(0x00);
    }

    [Fact]
    public void Parse_WithPositiveActiveEnergy_ShouldParseValue()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [0x43, 0x33, 0x34, 0x33, 0x78, 0x89, 0x9A, 0x35];
        var frame = BuildFrame(address, 0x91, data);

        var result = _parser.Parse(frame) as Dlt645ParsedFrame;

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.DataIdentifier.Should().Be(0x00010010);
        result.DataIdentifierInfo.Should().BeNull();
    }

    [Fact]
    public void Parse_WithTotalPositiveActiveEnergy_ShouldParseCorrectly()
    {
        byte[] address = [0x99, 0x99, 0x99, 0x99, 0x99, 0x99];
        byte[] data = [0x43, 0x33, 0x43, 0x33, 0x78, 0x89, 0x9A, 0x35];
        var frame = BuildFrame(address, 0x91, data);

        var result = _parser.Parse(frame) as Dlt645ParsedFrame;

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.DataIdentifier.Should().Be(0x00100010);
    }

    [Fact]
    public void Parse_DataFieldRawBytes_ShouldContainActualDataNotFrameHeader()
    {
        // 用户场景: 68 39 02 50 79 08 13 68 11 04 33 32 35 33 d1 16
        byte[] address = [0x39, 0x02, 0x50, 0x79, 0x08, 0x13];
        byte[] data = [0x33, 0x32, 0x35, 0x33]; // 数据域 (位置 10-13)
        var frame = BuildFrame(address, 0x11, data); // 0x11 = 读数据请求

        var result = _parser.Parse(frame) as Dlt645ParsedFrame;

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();

        // 找到数据域字段
        var dataField = result.Fields.FirstOrDefault(f => f.Name == "数据域");
        dataField.Should().NotBeNull();

        // 数据域的 RawBytes 应该是 [0x33, 0x32, 0x35, 0x33]，不是帧头的 0x68
        dataField!.RawBytes.Should().BeEquivalentTo(data);
        dataField.RawBytes[0].Should().Be(0x33, "数据域第一个字节应为 0x33，不是帧头 0x68");

        // StartIndex 应该是 10 (相对于整个帧的绝对位置)
        dataField.StartIndex.Should().Be(10);
        dataField.Length.Should().Be(4);
    }

    [Fact]
    public void Parse_WithCustomFieldDefinition_ShouldParseDataFieldSubFields()
    {
        // Arrange - 创建带自定义字段定义的解析器
        var definition = new FrameDefinition
        {
            Name = "DL/T 645-2007",
            ProtocolType = ProtocolType.Dlt645,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Field1",
                    StartIndex = 0,  // 数据域内第一个字节
                    Length = 1,
                    DataType = DataType.UInt8
                }
            ]
        };
        var parser = new Dlt645Parser(definition);

        // 测试帧: 68 39 02 50 79 08 13 68 11 04 33 32 35 33 d1 16
        byte[] address = [0x39, 0x02, 0x50, 0x79, 0x08, 0x13];
        byte[] data = [0x33, 0x32, 0x35, 0x33];
        var frame = BuildFrame(address, 0x11, data);

        // Act
        var result = parser.Parse(frame) as Dlt645ParsedFrame;

        // Assert
        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();

        // 应该有 Field1 字段
        var field1 = result.Fields.FirstOrDefault(f => f.Name == "Field1");
        field1.Should().NotBeNull();
        // Field1 解析的是解码后的数据（原始数据 0x33 - 0x33 = 0x00）
        field1!.Value.Should().Be((byte)0x00);
        field1.StartIndex.Should().Be(10); // 数据域起始位置

        // 应该有剩余数据字段
        var remainingField = result.Fields.FirstOrDefault(f => f.Name == "剩余数据");
        remainingField.Should().NotBeNull();
        remainingField!.Length.Should().Be(3); // 4字节数据域 - 1字节Field1 = 3字节剩余
    }

    [Fact]
    public void Parse_WithMultipleCustomFields_ShouldParseAllAndShowRemaining()
    {
        // Arrange
        var definition = new FrameDefinition
        {
            Name = "DL/T 645-2007",
            ProtocolType = ProtocolType.Dlt645,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Byte0",
                    StartIndex = 0,
                    Length = 1,
                    DataType = DataType.UInt8
                },
                new FieldDefinition
                {
                    Name = "Byte2",
                    StartIndex = 2,
                    Length = 1,
                    DataType = DataType.UInt8
                }
            ]
        };
        var parser = new Dlt645Parser(definition);

        byte[] address = [0x39, 0x02, 0x50, 0x79, 0x08, 0x13];
        byte[] data = [0x33, 0x34, 0x35, 0x36]; // 解码后: 0x00, 0x01, 0x02, 0x03
        var frame = BuildFrame(address, 0x11, data);

        // Act
        var result = parser.Parse(frame) as Dlt645ParsedFrame;

        // Assert
        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();

        var byte0 = result.Fields.FirstOrDefault(f => f.Name == "Byte0");
        byte0.Should().NotBeNull();
        byte0!.Value.Should().Be((byte)0x00);

        var byte2 = result.Fields.FirstOrDefault(f => f.Name == "Byte2");
        byte2.Should().NotBeNull();
        byte2!.Value.Should().Be((byte)0x02);

        // 剩余数据应包含索引1和3的字节
        var remaining = result.Fields.FirstOrDefault(f => f.Name == "剩余数据");
        remaining.Should().NotBeNull();
        remaining!.Length.Should().Be(2);
    }
}

public class Dlt645ControlCodeTests
{
    [Fact]
    public void ControlCode_WithReadDataRequest_ShouldParseCorrectly()
    {
        var code = new Dlt645ControlCode(0x11);

        code.FunctionCode.Should().Be(Dlt645FunctionCode.ReadData);
        code.IsResponse.Should().BeFalse();
        code.IsError.Should().BeFalse();
        code.HasFollowFrame.Should().BeFalse();
    }

    [Fact]
    public void ControlCode_WithReadDataResponse_ShouldParseCorrectly()
    {
        var code = new Dlt645ControlCode(0x91);

        code.FunctionCode.Should().Be(Dlt645FunctionCode.ReadData);
        code.IsResponse.Should().BeTrue();
        code.IsError.Should().BeFalse();
    }

    [Fact]
    public void ControlCode_WithErrorResponse_ShouldParseCorrectly()
    {
        var code = new Dlt645ControlCode(0xD1);

        code.FunctionCode.Should().Be(Dlt645FunctionCode.ReadData);
        code.IsResponse.Should().BeTrue();
        code.IsError.Should().BeTrue();
    }

    [Fact]
    public void ControlCode_WithFollowFrame_ShouldParseCorrectly()
    {
        var code = new Dlt645ControlCode(0xB1);

        code.HasFollowFrame.Should().BeTrue();
        code.IsResponse.Should().BeTrue();
    }

    [Theory]
    [InlineData(Dlt645FunctionCode.ReadData, 0x11)]
    [InlineData(Dlt645FunctionCode.ReadFollowData, 0x12)]
    [InlineData(Dlt645FunctionCode.ReadAddress, 0x13)]
    [InlineData(Dlt645FunctionCode.WriteData, 0x14)]
    [InlineData(Dlt645FunctionCode.WriteAddress, 0x15)]
    public void ControlCode_ShouldParseFunctionCode(Dlt645FunctionCode expected, byte value)
    {
        var code = new Dlt645ControlCode(value);
        code.FunctionCode.Should().Be(expected);
    }
}

public class Dlt645DataDictionaryTests
{
    [Fact]
    public void GetIdentifier_WithValidCode_ShouldReturnIdentifier()
    {
        var identifier = Dlt645DataDictionary.GetIdentifier(0x00000000);

        identifier.Should().NotBeNull();
        identifier!.Name.Should().Be("组合有功总电能");
        identifier.Unit.Should().Be("kWh");
        identifier.DecimalPlaces.Should().Be(2);
    }

    [Fact]
    public void GetIdentifier_WithUnknownCode_ShouldReturnNull()
    {
        var identifier = Dlt645DataDictionary.GetIdentifier(0xFFFFFFFF);

        identifier.Should().BeNull();
    }

    [Fact]
    public void GetIdentifier_WithByteParameters_ShouldWork()
    {
        var identifier = Dlt645DataDictionary.GetIdentifier(0x00, 0x01, 0x01, 0x02);

        identifier.Should().NotBeNull();
        identifier!.Name.Should().Be("A相电压");
    }

    [Fact]
    public void GetDataName_WithValidCode_ShouldReturnName()
    {
        var name = Dlt645DataDictionary.GetDataName(0x02010100);

        name.Should().Be("A相电压");
    }

    [Fact]
    public void GetDataName_WithUnknownCode_ShouldReturnUnknown()
    {
        var name = Dlt645DataDictionary.GetDataName(0xFFFFFFFF);

        name.Should().Contain("未知数据项");
    }

    [Fact]
    public void GetAllIdentifiers_ShouldReturnNonEmptyDictionary()
    {
        var identifiers = Dlt645DataDictionary.GetAllIdentifiers();

        identifiers.Should().NotBeEmpty();
        identifiers.Count.Should().BeGreaterThan(20);
    }
}

public class Dlt645ErrorCodeTests
{
    [Fact]
    public void GetDescription_ShouldReturnCorrectDescription()
    {
        Dlt645ErrorCode.NoData.GetDescription().Should().Be("无请求数据");
        Dlt645ErrorCode.PasswordError.GetDescription().Should().Be("密码错/未授权");
    }

    [Fact]
    public void GetAllErrors_WithMultipleErrors_ShouldReturnAll()
    {
        byte errorByte = 0x06;

        var errors = Dlt645ErrorCodeExtensions.GetAllErrors(errorByte);

        errors.Should().HaveCount(2);
        errors.Should().Contain("无请求数据");
        errors.Should().Contain("密码错/未授权");
    }

    [Fact]
    public void GetAllErrors_WithNoError_ShouldReturnEmpty()
    {
        var errors = Dlt645ErrorCodeExtensions.GetAllErrors(0x00);

        errors.Should().BeEmpty();
    }
}
