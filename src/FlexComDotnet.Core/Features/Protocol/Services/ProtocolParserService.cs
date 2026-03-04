using System.Text.Json;
using System.Text.Json.Serialization;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services.Parsers;

namespace FlexComDotnet.Core.Features.Protocol.Services;

/// <summary>
/// 协议解析服务实现
/// </summary>
public class ProtocolParserService : IProtocolParserService
{
    private readonly IChecksumService _checksumService;
    private readonly Dictionary<string, IProtocolParser> _parsers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public event EventHandler<ParserRegisteredEventArgs>? ParserRegistered;
    public event EventHandler<ParserRemovedEventArgs>? ParserRemoved;

    public ProtocolParserService(IChecksumService checksumService)
    {
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
    }

    public IReadOnlyList<IProtocolParser> GetAllParsers()
    {
        lock (_lock)
        {
            return _parsers.Values.ToList();
        }
    }

    public IProtocolParser? GetParser(string name)
    {
        lock (_lock)
        {
            return _parsers.GetValueOrDefault(name);
        }
    }

    public IProtocolParser RegisterDefinition(FrameDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("协议名称不能为空", nameof(definition));

        lock (_lock)
        {
            IProtocolParser parser = definition.ProtocolType switch
            {
                ProtocolType.Dlt645 => new Dlt645Parser(definition),
                _ => new ConfigurableParser(definition, _checksumService)
            };
            _parsers[definition.Name] = parser;
            _definitions[definition.Name] = definition;

            ParserRegistered?.Invoke(this, new ParserRegisteredEventArgs(parser));
            return parser;
        }
    }

    public void LoadDefinitions(IEnumerable<FrameDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        lock (_lock)
        {
            foreach (var definition in definitions)
            {
                if (!string.IsNullOrWhiteSpace(definition.Name))
                {
                    IProtocolParser parser = definition.ProtocolType switch
                    {
                        ProtocolType.Dlt645 => new Dlt645Parser(definition),
                        _ => new ConfigurableParser(definition, _checksumService)
                    };
                    _parsers[definition.Name] = parser;
                    _definitions[definition.Name] = definition;
                }
            }
        }
    }

    public bool RemoveParser(string name)
    {
        lock (_lock)
        {
            if (_parsers.Remove(name))
            {
                _definitions.Remove(name);
                ParserRemoved?.Invoke(this, new ParserRemovedEventArgs(name));
                return true;
            }
            return false;
        }
    }

    public ParsedFrame Parse(string parserName, byte[] frame)
    {
        var parser = GetParser(parserName);
        if (parser == null)
        {
            return new ParsedFrame
            {
                RawData = frame,
                IsValid = false,
                ErrorMessage = $"未找到解析器: {parserName}"
            };
        }

        return parser.Parse(frame);
    }

    public ParsedFrame? AutoParse(byte[] frame)
    {
        lock (_lock)
        {
            foreach (var parser in _parsers.Values.Where(p => p.Definition.IsEnabled))
            {
                if (parser.Validate(frame))
                {
                    var result = parser.Parse(frame);
                    if (result.IsValid)
                        return result;
                }
            }
        }
        return null;
    }

    public IReadOnlyList<FrameDefinition> GetAllDefinitions()
    {
        lock (_lock)
        {
            return _definitions.Values.ToList();
        }
    }

    public async Task SaveDefinitionAsync(FrameDefinition definition, string filePath)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        definition.ModifiedAt = DateTime.Now;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(definition, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<FrameDefinition?> LoadDefinitionAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<FrameDefinition>(json, JsonOptions);
    }
}
