using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Protocol;

public class ProtocolParserServiceTests
{
    private readonly IChecksumService _checksumService = new ChecksumService();

    [Fact]
    public void RegisterDefinition_ValidDefinition_CreatesParser()
    {
        var service = new ProtocolParserService(_checksumService);
        var definition = new FrameDefinition
        {
            Name = "TestProtocol",
            Description = "Test protocol description"
        };

        var parser = service.RegisterDefinition(definition);

        parser.Should().NotBeNull();
        parser.Name.Should().Be("TestProtocol");
        service.GetAllParsers().Should().HaveCount(1);
    }

    [Fact]
    public void RegisterDefinition_EmptyName_ThrowsException()
    {
        var service = new ProtocolParserService(_checksumService);
        var definition = new FrameDefinition { Name = "" };

        var act = () => service.RegisterDefinition(definition);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetParser_ExistingParser_ReturnsParser()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition { Name = "TestProtocol" });

        var parser = service.GetParser("TestProtocol");

        parser.Should().NotBeNull();
        parser!.Name.Should().Be("TestProtocol");
    }

    [Fact]
    public void GetParser_NonExistingParser_ReturnsNull()
    {
        var service = new ProtocolParserService(_checksumService);

        var parser = service.GetParser("NonExisting");

        parser.Should().BeNull();
    }

    [Fact]
    public void GetParser_CaseInsensitive_ReturnsParser()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition { Name = "TestProtocol" });

        var parser = service.GetParser("TESTPROTOCOL");

        parser.Should().NotBeNull();
    }

    [Fact]
    public void RemoveParser_ExistingParser_ReturnsTrue()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition { Name = "TestProtocol" });

        var result = service.RemoveParser("TestProtocol");

        result.Should().BeTrue();
        service.GetAllParsers().Should().BeEmpty();
    }

    [Fact]
    public void RemoveParser_NonExistingParser_ReturnsFalse()
    {
        var service = new ProtocolParserService(_checksumService);

        var result = service.RemoveParser("NonExisting");

        result.Should().BeFalse();
    }

    [Fact]
    public void Parse_ValidFrame_ReturnsResult()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "TestProtocol",
            MinFrameLength = 2,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Value",
                    StartIndex = 0,
                    DataType = DataType.UInt16,
                    Endianness = Endianness.BigEndian
                }
            ]
        });

        var result = service.Parse("TestProtocol", [0x12, 0x34]);

        result.IsValid.Should().BeTrue();
        result.GetValue<ushort>("Value").Should().Be(0x1234);
    }

    [Fact]
    public void Parse_NonExistingParser_ReturnsInvalidResult()
    {
        var service = new ProtocolParserService(_checksumService);

        var result = service.Parse("NonExisting", [0x01, 0x02]);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未找到解析器");
    }

    [Fact]
    public void AutoParse_MatchingProtocol_ReturnsResult()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "Protocol1",
            Header = "AA BB",
            MinFrameLength = 4
        });
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "Protocol2",
            Header = "CC DD",
            MinFrameLength = 4
        });

        var result = service.AutoParse([0xCC, 0xDD, 0x01, 0x02]);

        result.Should().NotBeNull();
        result!.ProtocolName.Should().Be("Protocol2");
    }

    [Fact]
    public void AutoParse_NoMatchingProtocol_ReturnsNull()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "Protocol1",
            Header = "AA BB",
            MinFrameLength = 4
        });

        var result = service.AutoParse([0xFF, 0xFF, 0x01, 0x02]);

        result.Should().BeNull();
    }

    [Fact]
    public void AutoParse_DisabledProtocol_IsSkipped()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "DisabledProtocol",
            Header = "AA BB",
            MinFrameLength = 4,
            IsEnabled = false
        });

        var result = service.AutoParse([0xAA, 0xBB, 0x01, 0x02]);

        result.Should().BeNull();
    }

    [Fact]
    public void GetAllDefinitions_ReturnsAllDefinitions()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition { Name = "Protocol1" });
        service.RegisterDefinition(new FrameDefinition { Name = "Protocol2" });

        var definitions = service.GetAllDefinitions();

        definitions.Should().HaveCount(2);
    }

    [Fact]
    public void ParserRegistered_Event_IsFired()
    {
        var service = new ProtocolParserService(_checksumService);
        IProtocolParser? registeredParser = null;
        service.ParserRegistered += (_, e) => registeredParser = e.Parser;

        service.RegisterDefinition(new FrameDefinition { Name = "TestProtocol" });

        registeredParser.Should().NotBeNull();
        registeredParser!.Name.Should().Be("TestProtocol");
    }

    [Fact]
    public void ParserRemoved_Event_IsFired()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition { Name = "TestProtocol" });
        string? removedName = null;
        service.ParserRemoved += (_, e) => removedName = e.ParserName;

        service.RemoveParser("TestProtocol");

        removedName.Should().Be("TestProtocol");
    }

    [Fact]
    public async Task SaveAndLoadDefinition_RoundTrip_PreservesData()
    {
        var service = new ProtocolParserService(_checksumService);
        var definition = new FrameDefinition
        {
            Name = "TestProtocol",
            Description = "Test description",
            Header = "AA BB",
            Trailer = "FF",
            MinFrameLength = 10,
            Fields =
            [
                new FieldDefinition
                {
                    Name = "Field1",
                    StartIndex = 2,
                    DataType = DataType.UInt16,
                    Endianness = Endianness.BigEndian,
                    BitFields =
                    [
                        new BitFieldDefinition
                        {
                            Name = "Flag1",
                            BitOffset = 0,
                            BitCount = 1
                        }
                    ]
                }
            ]
        };

        var tempFile = Path.GetTempFileName();
        try
        {
            await service.SaveDefinitionAsync(definition, tempFile);
            var loaded = await service.LoadDefinitionAsync(tempFile);

            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("TestProtocol");
            loaded.Description.Should().Be("Test description");
            loaded.Header.Should().Be("AA BB");
            loaded.Trailer.Should().Be("FF");
            loaded.MinFrameLength.Should().Be(10);
            loaded.Fields.Should().HaveCount(1);
            loaded.Fields[0].Name.Should().Be("Field1");
            loaded.Fields[0].DataType.Should().Be(DataType.UInt16);
            loaded.Fields[0].BitFields.Should().HaveCount(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadDefinition_NonExistingFile_ReturnsNull()
    {
        var service = new ProtocolParserService(_checksumService);

        var result = await service.LoadDefinitionAsync("non_existing_file.json");

        result.Should().BeNull();
    }

    [Fact]
    public void RegisterDefinition_SameName_OverwritesPrevious()
    {
        var service = new ProtocolParserService(_checksumService);
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "TestProtocol",
            Description = "First"
        });
        service.RegisterDefinition(new FrameDefinition
        {
            Name = "TestProtocol",
            Description = "Second"
        });

        var parser = service.GetParser("TestProtocol");

        parser!.Description.Should().Be("Second");
        service.GetAllParsers().Should().HaveCount(1);
    }
}
