using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services.Parsers;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Protocol;

public class ConfigurableParserTests
{
    private readonly IChecksumService _checksumService = new ChecksumService();

    [Fact]
    public void Parse_SimpleFrame_ExtractsFieldsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "TestProtocol",
            Header = "AA BB",
            MinFrameLength = 6,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Command",
                    StartIndex = 2,
                    DataType = DataType.UInt8
                },
                new FieldDefinition
                {
                    Name = "Data",
                    StartIndex = 3,
                    Length = 2,
                    DataType = DataType.UInt16,
                    Endianness = Endianness.BigEndian
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0xAA, 0xBB, 0x01, 0x12, 0x34, 0xFF];

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.Fields.Should().HaveCount(2);
        result.GetValue<byte>("Command").Should().Be(0x01);
        result.GetValue<ushort>("Data").Should().Be(0x1234);
    }

    [Fact]
    public void Parse_WithLittleEndian_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "LittleEndianTest",
            MinFrameLength = 4,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Value",
                    StartIndex = 0,
                    Length = 4,
                    DataType = DataType.UInt32,
                    Endianness = Endianness.LittleEndian
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0x78, 0x56, 0x34, 0x12];

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.GetValue<uint>("Value").Should().Be(0x12345678);
    }

    [Fact]
    public void Parse_WithBigEndian_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "BigEndianTest",
            MinFrameLength = 4,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Value",
                    StartIndex = 0,
                    Length = 4,
                    DataType = DataType.UInt32,
                    Endianness = Endianness.BigEndian
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0x12, 0x34, 0x56, 0x78];

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.GetValue<uint>("Value").Should().Be(0x12345678);
    }

    [Fact]
    public void Parse_WithBitFields_ExtractsBitsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "BitFieldTest",
            MinFrameLength = 1,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "StatusByte",
                    StartIndex = 0,
                    DataType = DataType.UInt8,
                    BitFields =
                    [
                        new BitFieldDefinition
                        {
                            Name = "Flag1",
                            BitOffset = 0,
                            BitCount = 1
                        },
                        new BitFieldDefinition
                        {
                            Name = "Flag2",
                            BitOffset = 1,
                            BitCount = 1
                        },
                        new BitFieldDefinition
                        {
                            Name = "Mode",
                            BitOffset = 4,
                            BitCount = 4
                        }
                    ]
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0b1010_0011];

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var statusField = result.GetField("StatusByte");
        statusField.Should().NotBeNull();
        statusField!.BitFields.Should().HaveCount(3);

        var flag1 = statusField.BitFields.Find(bf => bf.Name == "Flag1");
        flag1!.Value.Should().Be(1);
        flag1.BoolValue.Should().BeTrue();

        var flag2 = statusField.BitFields.Find(bf => bf.Name == "Flag2");
        flag2!.Value.Should().Be(1);

        var mode = statusField.BitFields.Find(bf => bf.Name == "Mode");
        mode!.Value.Should().Be(0b1010);
    }

    [Fact]
    public void Parse_WithMask_ExtractsBitsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "MaskTest",
            MinFrameLength = 1,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "StatusByte",
                    StartIndex = 0,
                    DataType = DataType.UInt8,
                    BitFields =
                    [
                        new BitFieldDefinition
                        {
                            Name = "LowNibble",
                            Mask = 0x0F
                        },
                        new BitFieldDefinition
                        {
                            Name = "HighNibble",
                            Mask = 0xF0
                        }
                    ]
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0xAB];

        var result = parser.Parse(frame);

        var statusField = result.GetField("StatusByte");
        var lowNibble = statusField!.BitFields.Find(bf => bf.Name == "LowNibble");
        lowNibble!.Value.Should().Be(0x0B);

        var highNibble = statusField.BitFields.Find(bf => bf.Name == "HighNibble");
        highNibble!.Value.Should().Be(0x0A);
    }

    [Fact]
    public void Validate_InvalidHeader_ReturnsFalse()
    {
        var definition = new FrameDefinition
        {
            Name = "HeaderTest",
            Header = "AA BB",
            MinFrameLength = 4
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0xCC, 0xDD, 0x01, 0x02];

        parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidHeader_ReturnsTrue()
    {
        var definition = new FrameDefinition
        {
            Name = "HeaderTest",
            Header = "AA BB",
            MinFrameLength = 4
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0xAA, 0xBB, 0x01, 0x02];

        parser.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTrailer_ValidatesCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "TrailerTest",
            Header = "AA",
            Trailer = "FF",
            MinFrameLength = 4
        };

        var parser = new ConfigurableParser(definition, _checksumService);

        byte[] validFrame = [0xAA, 0x01, 0x02, 0xFF];
        byte[] invalidFrame = [0xAA, 0x01, 0x02, 0xEE];

        parser.Validate(validFrame).Should().BeTrue();
        parser.Validate(invalidFrame).Should().BeFalse();
    }

    [Fact]
    public void Validate_FrameTooShort_ReturnsFalse()
    {
        var definition = new FrameDefinition
        {
            Name = "LengthTest",
            MinFrameLength = 10
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0x01, 0x02, 0x03];

        parser.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void TryExtractFrame_WithHeader_ExtractsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "ExtractTest",
            Header = "AA BB",
            MinFrameLength = 5
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] buffer = [0x00, 0x00, 0xAA, 0xBB, 0x01, 0x02, 0x03, 0xFF];

        var success = parser.TryExtractFrame(buffer, out var frame, out var consumed);

        success.Should().BeTrue();
        frame.Should().Equal([0xAA, 0xBB, 0x01, 0x02, 0x03]);
        consumed.Should().Be(7);
    }

    [Fact]
    public void TryExtractFrame_WithTrailer_ExtractsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "TrailerExtractTest",
            Header = "AA",
            Trailer = "FF"
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] buffer = [0xAA, 0x01, 0x02, 0xFF, 0x00, 0x00];

        var success = parser.TryExtractFrame(buffer, out var frame, out var consumed);

        success.Should().BeTrue();
        frame.Should().Equal([0xAA, 0x01, 0x02, 0xFF]);
        consumed.Should().Be(4);
    }

    [Fact]
    public void Parse_FloatValue_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "FloatTest",
            MinFrameLength = 4,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Temperature",
                    StartIndex = 0,
                    DataType = DataType.Float,
                    Endianness = Endianness.LittleEndian
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        float expected = 25.5f;
        byte[] frame = BitConverter.GetBytes(expected);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.GetValue<float>("Temperature").Should().BeApproximately(expected, 0.001f);
    }

    [Fact]
    public void Parse_AsciiString_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "StringTest",
            MinFrameLength = 5,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Message",
                    StartIndex = 0,
                    Length = 5,
                    DataType = DataType.AsciiString
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0x48, 0x65, 0x6C, 0x6C, 0x6F];

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.GetValue<string>("Message").Should().Be("Hello");
    }

    [Fact]
    public void Parse_WithChecksum_ValidatesCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "ChecksumTest",
            Header = "AA",
            MinFrameLength = 5,
            ChecksumConfig = new ChecksumConfig
            {
                Algorithm = ChecksumAlgorithmType.Sum8,
                StartIndex = -1,
                Length = 1,
                CalculateStartIndex = 1,
                CalculateEndIndex = -1
            },
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Data1",
                    StartIndex = 1,
                    DataType = DataType.UInt8
                },
                new FieldDefinition
                {
                    Name = "Data2",
                    StartIndex = 2,
                    DataType = DataType.UInt8
                },
                new FieldDefinition
                {
                    Name = "Data3",
                    StartIndex = 3,
                    DataType = DataType.UInt8
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] validFrame = [0xAA, 0x01, 0x02, 0x03, 0x06];
        byte[] invalidFrame = [0xAA, 0x01, 0x02, 0x03, 0xFF];

        var validResult = parser.Parse(validFrame);
        var invalidResult = parser.Parse(invalidFrame);

        validResult.ChecksumValid.Should().BeTrue();
        invalidResult.ChecksumValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_DisabledField_IsSkipped()
    {
        var definition = new FrameDefinition
        {
            Name = "DisabledFieldTest",
            MinFrameLength = 2,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "EnabledField",
                    StartIndex = 0,
                    DataType = DataType.UInt8,
                    IsEnabled = true
                },
                new FieldDefinition
                {
                    Name = "DisabledField",
                    StartIndex = 1,
                    DataType = DataType.UInt8,
                    IsEnabled = false
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = [0x01, 0x02];

        var result = parser.Parse(frame);

        result.Fields.Should().HaveCount(1);
        result.GetField("EnabledField").Should().NotBeNull();
        result.GetField("DisabledField").Should().BeNull();
    }

    [Fact]
    public void Parse_AllDataTypes_ConvertCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "AllTypesTest",
            MinFrameLength = 30,
            Fields =
            [
                new FieldDefinition { Name = "UInt8", StartIndex = 0, DataType = DataType.UInt8 },
                new FieldDefinition { Name = "Int8", StartIndex = 1, DataType = DataType.Int8 },
                new FieldDefinition { Name = "UInt16", StartIndex = 2, DataType = DataType.UInt16, Endianness = Endianness.LittleEndian },
                new FieldDefinition { Name = "Int16", StartIndex = 4, DataType = DataType.Int16, Endianness = Endianness.LittleEndian },
                new FieldDefinition { Name = "UInt32", StartIndex = 6, DataType = DataType.UInt32, Endianness = Endianness.LittleEndian },
                new FieldDefinition { Name = "Int32", StartIndex = 10, DataType = DataType.Int32, Endianness = Endianness.LittleEndian },
                new FieldDefinition { Name = "Bool", StartIndex = 14, DataType = DataType.Bool }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] frame = new byte[30];
        frame[0] = 255;
        frame[1] = unchecked((byte)-1);
        BitConverter.GetBytes((ushort)1234).CopyTo(frame, 2);
        BitConverter.GetBytes((short)-1234).CopyTo(frame, 4);
        BitConverter.GetBytes((uint)123456).CopyTo(frame, 6);
        BitConverter.GetBytes(-123456).CopyTo(frame, 10);
        frame[14] = 1;

        var result = parser.Parse(frame);

        result.GetValue<byte>("UInt8").Should().Be(255);
        result.GetValue<sbyte>("Int8").Should().Be(-1);
        result.GetValue<ushort>("UInt16").Should().Be(1234);
        result.GetValue<short>("Int16").Should().Be(-1234);
        result.GetValue<uint>("UInt32").Should().Be(123456);
        result.GetValue<int>("Int32").Should().Be(-123456);
        result.GetValue<bool>("Bool").Should().BeTrue();
    }
}
