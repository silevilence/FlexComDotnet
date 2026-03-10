using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Protocol.Services.Parsers;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Protocol;

public class FrameBuilderTests
{
    private readonly IChecksumService _checksumService = new ChecksumService();

    [Fact]
    public void BuildFrame_SimpleFrame_WithHeaderAndFields()
    {
        var definition = new FrameDefinition
        {
            Name = "TestProtocol",
            Header = "AA BB",
            MinFrameLength = 5,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Command",
                    StartIndex = 0,
                    DataType = DataType.UInt8,
                    Length = 1
                },
                new FieldDefinition
                {
                    Name = "Data",
                    StartIndex = 1,
                    Length = 2,
                    DataType = DataType.UInt16,
                    Endianness = Endianness.BigEndian
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["Command"] = (byte)0x01,
            ["Data"] = (ushort)0x1234
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().NotBeNull();
        result.Should().Equal([0xAA, 0xBB, 0x01, 0x12, 0x34]);
    }

    [Fact]
    public void BuildFrame_WithTrailer_AppendsTrailer()
    {
        var definition = new FrameDefinition
        {
            Name = "TrailerTest",
            Header = "AA",
            Trailer = "FF",
            MinFrameLength = 4,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Data1",
                    StartIndex = 0,
                    DataType = DataType.UInt8,
                    Length = 1
                },
                new FieldDefinition
                {
                    Name = "Data2",
                    StartIndex = 1,
                    DataType = DataType.UInt8,
                    Length = 1
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["Data1"] = (byte)0x01,
            ["Data2"] = (byte)0x02
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0xAA, 0x01, 0x02, 0xFF]);
    }

    [Fact]
    public void BuildFrame_WithLittleEndian_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "LittleEndianBuild",
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
        var fieldValues = new Dictionary<string, object>
        {
            ["Value"] = 0x12345678u
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0x78, 0x56, 0x34, 0x12]);
    }

    [Fact]
    public void BuildFrame_WithBigEndian_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "BigEndianBuild",
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
        var fieldValues = new Dictionary<string, object>
        {
            ["Value"] = 0x12345678u
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0x12, 0x34, 0x56, 0x78]);
    }

    [Fact]
    public void BuildFrame_WithChecksum_CalculatesAndAppends()
    {
        var definition = new FrameDefinition
        {
            Name = "ChecksumBuild",
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
                new FieldDefinition { Name = "Data1", StartIndex = 0, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Data2", StartIndex = 1, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Data3", StartIndex = 2, DataType = DataType.UInt8, Length = 1 }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["Data1"] = (byte)0x01,
            ["Data2"] = (byte)0x02,
            ["Data3"] = (byte)0x03
        };

        var result = parser.BuildFrame(fieldValues);

        // Header(AA) + 01 + 02 + 03 + checksum(01+02+03=06)
        result.Should().Equal([0xAA, 0x01, 0x02, 0x03, 0x06]);
    }

    [Fact]
    public void BuildFrame_Float_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "FloatBuild",
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
        var fieldValues = new Dictionary<string, object>
        {
            ["Temperature"] = expected
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal(BitConverter.GetBytes(expected));
    }

    [Fact]
    public void BuildFrame_AsciiString_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "StringBuild",
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
        var fieldValues = new Dictionary<string, object>
        {
            ["Message"] = "Hello"
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0x48, 0x65, 0x6C, 0x6C, 0x6F]);
    }

    [Fact]
    public void BuildFrame_DisabledField_IsSkipped()
    {
        var definition = new FrameDefinition
        {
            Name = "DisabledFieldBuild",
            MinFrameLength = 2,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "EnabledField",
                    StartIndex = 0,
                    DataType = DataType.UInt8,
                    Length = 1,
                    IsEnabled = true
                },
                new FieldDefinition
                {
                    Name = "DisabledField",
                    StartIndex = 1,
                    DataType = DataType.UInt8,
                    Length = 1,
                    IsEnabled = false
                }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["EnabledField"] = (byte)0x42,
            ["DisabledField"] = (byte)0xFF
        };

        var result = parser.BuildFrame(fieldValues);

        // Disabled field position should be 0x00 (not filled)
        result.Should().Equal([0x42, 0x00]);
    }

    [Fact]
    public void BuildFrame_MissingFieldValue_UsesZero()
    {
        var definition = new FrameDefinition
        {
            Name = "MissingValueTest",
            MinFrameLength = 3,
            Fields =
            [
                new FieldDefinition { Name = "Field1", StartIndex = 0, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Field2", StartIndex = 1, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Field3", StartIndex = 2, DataType = DataType.UInt8, Length = 1 }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["Field1"] = (byte)0x01,
            // Field2 missing - should default to 0
            ["Field3"] = (byte)0x03
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0x01, 0x00, 0x03]);
    }

    [Fact]
    public void BuildFrame_Roundtrip_ParseAndBuildMatch()
    {
        var definition = new FrameDefinition
        {
            Name = "RoundtripTest",
            Header = "AA BB",
            Trailer = "FF",
            MinFrameLength = 7,
            Fields =
            [
                new FieldDefinition { Name = "Command", StartIndex = 0, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Length", StartIndex = 1, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Data", StartIndex = 2, Length = 2, DataType = DataType.UInt16, Endianness = Endianness.BigEndian }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        byte[] originalFrame = [0xAA, 0xBB, 0x01, 0x02, 0x12, 0x34, 0xFF];

        // Parse the frame
        var parsed = parser.Parse(originalFrame);
        parsed.IsValid.Should().BeTrue();

        // Build a frame with parsed values
        var fieldValues = new Dictionary<string, object>();
        foreach (var field in parsed.Fields)
        {
            if (field.Value != null)
                fieldValues[field.Name] = field.Value;
        }

        var rebuilt = parser.BuildFrame(fieldValues);

        rebuilt.Should().Equal(originalFrame);
    }

    [Fact]
    public void BuildFrame_WithStringFieldValues_ConvertsFromString()
    {
        var definition = new FrameDefinition
        {
            Name = "StringValueTest",
            MinFrameLength = 3,
            Fields =
            [
                new FieldDefinition { Name = "Byte1", StartIndex = 0, DataType = DataType.UInt8, Length = 1 },
                new FieldDefinition { Name = "Word1", StartIndex = 1, Length = 2, DataType = DataType.UInt16, Endianness = Endianness.BigEndian }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        // String values (as user might input from UI)
        var fieldValues = new Dictionary<string, object>
        {
            ["Byte1"] = "255",
            ["Word1"] = "4660" // 0x1234
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0xFF, 0x12, 0x34]);
    }

    [Fact]
    public void BuildFrame_HexStringInput_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "HexInputTest",
            MinFrameLength = 3,
            Fields =
            [
                new FieldDefinition { Name = "RawData", StartIndex = 0, Length = 3, DataType = DataType.Bytes }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["RawData"] = "01 02 03"
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0x01, 0x02, 0x03]);
    }

    [Fact]
    public void BuildFrame_Int8Negative_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "Int8Test",
            MinFrameLength = 1,
            Fields =
            [
                new FieldDefinition { Name = "SignedByte", StartIndex = 0, DataType = DataType.Int8, Length = 1 }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["SignedByte"] = (sbyte)-1
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0xFF]);
    }

    [Fact]
    public void BuildFrame_Int16Negative_LittleEndian()
    {
        var definition = new FrameDefinition
        {
            Name = "Int16Test",
            MinFrameLength = 2,
            Fields =
            [
                new FieldDefinition { Name = "Value", StartIndex = 0, Length = 2, DataType = DataType.Int16, Endianness = Endianness.LittleEndian }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["Value"] = (short)-1234
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal(BitConverter.GetBytes((short)-1234));
    }

    [Fact]
    public void BuildFrame_Double_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "DoubleTest",
            MinFrameLength = 8,
            Fields =
            [
                new FieldDefinition { Name = "Value", StartIndex = 0, Length = 8, DataType = DataType.Double, Endianness = Endianness.LittleEndian }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        double expected = 3.14159;
        var fieldValues = new Dictionary<string, object>
        {
            ["Value"] = expected
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal(BitConverter.GetBytes(expected));
    }

    [Fact]
    public void BuildFrame_Bool_ConvertsCorrectly()
    {
        var definition = new FrameDefinition
        {
            Name = "BoolTest",
            MinFrameLength = 2,
            Fields =
            [
                new FieldDefinition { Name = "FlagTrue", StartIndex = 0, DataType = DataType.Bool, Length = 1 },
                new FieldDefinition { Name = "FlagFalse", StartIndex = 1, DataType = DataType.Bool, Length = 1 }
            ]
        };

        var parser = new ConfigurableParser(definition, _checksumService);
        var fieldValues = new Dictionary<string, object>
        {
            ["FlagTrue"] = true,
            ["FlagFalse"] = false
        };

        var result = parser.BuildFrame(fieldValues);

        result.Should().Equal([0x01, 0x00]);
    }
}
