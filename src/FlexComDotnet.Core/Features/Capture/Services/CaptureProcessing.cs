using System.Text.Json;
using System.Text.Json.Serialization;
using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.Capture.Services;

/// <summary>Shared presentation/analysis of projections. Source-specific exports retain their own schemas.</summary>
public sealed class CaptureProcessing(IProtocolParserService parsers)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public string Format(CaptureRecord record, bool hex) => hex
        ? HexHelper.BytesToHexString(record.Data) : HexHelper.BytesToAsciiString(record.Data, '.');

    public bool Matches(CaptureRecord record, string search) => string.IsNullOrWhiteSpace(search)
        || record.StreamId.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Format(record, true).Contains(search, StringComparison.OrdinalIgnoreCase)
        || Format(record, false).Contains(search, StringComparison.OrdinalIgnoreCase);

    public ParsedFrame Parse(CaptureRecord record, string parserName)
    {
        if (record.Integrity != CaptureIntegrity.Complete || record.Direction == CaptureDirection.Control)
            throw new InvalidOperationException("Only complete business data may be parsed.");
        return parsers.Parse(parserName, record.Data.ToArray());
    }

    public ParsedFrame Parse(CaptureFrame frame, string parserName) => parsers.Parse(parserName, frame.Data.ToArray());

    public string ExportJson(IEnumerable<CaptureRecord> records) => JsonSerializer.Serialize(records, s_jsonOptions);

    public IReadOnlyList<CaptureRecord> ImportJson(string json) =>
        JsonSerializer.Deserialize<CaptureRecord[]>(json, s_jsonOptions) ?? [];
}
