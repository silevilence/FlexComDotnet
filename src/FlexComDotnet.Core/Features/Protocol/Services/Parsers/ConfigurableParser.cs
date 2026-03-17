using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.Protocol.Services.Parsers;

/// <summary>
/// 基于配置的通用帧解析器
/// </summary>
public class ConfigurableParser : IProtocolParser
{
    private readonly IChecksumService _checksumService;
    private readonly byte[] _headerBytes;
    private readonly byte[] _trailerBytes;

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public FrameDefinition Definition { get; }

    public ConfigurableParser(FrameDefinition definition, IChecksumService checksumService)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));

        _headerBytes = ParseHexString(definition.Header);
        _trailerBytes = ParseHexString(definition.Trailer);
    }

    public ParsedFrame Parse(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var result = new ParsedFrame
        {
            RawData = frame,
            ProtocolName = Name
        };

        var validationError = ValidateWithReason(frame);
        if (validationError != null)
        {
            result.IsValid = false;
            result.ErrorMessage = validationError;
            return result;
        }

        if (Definition.ChecksumConfig != null)
        {
            result.ChecksumValid = ValidateChecksum(frame);
            if (!result.ChecksumValid)
            {
                result.ErrorMessage = "校验失败";
            }
        }

        foreach (var fieldDef in Definition.Fields.Where(f => f.IsEnabled))
        {
            var parsedField = ParseField(frame, fieldDef);
            if (parsedField != null)
            {
                result.Fields.Add(parsedField);
            }
        }

        result.IsValid = true;
        return result;
    }

    public bool Validate(byte[] frame) => ValidateWithReason(frame) == null;

    private string? ValidateWithReason(byte[] frame)
    {
        if (frame == null || frame.Length == 0)
            return "帧数据为空";

        if (Definition.MinFrameLength > 0 && frame.Length < Definition.MinFrameLength)
            return $"帧长度不足: 最小 {Definition.MinFrameLength} 字节, 实际 {frame.Length} 字节";

        if (Definition.MaxFrameLength > 0 && frame.Length > Definition.MaxFrameLength)
            return $"帧长度超限: 最大 {Definition.MaxFrameLength} 字节, 实际 {frame.Length} 字节";

        if (_headerBytes.Length > 0)
        {
            if (frame.Length < _headerBytes.Length)
                return $"帧长度不足以包含帧头: 需要 {_headerBytes.Length} 字节";

            for (int i = 0; i < _headerBytes.Length; i++)
            {
                if (frame[i] != _headerBytes[i])
                    return $"帧头错误: 位置 {i} 期望 0x{_headerBytes[i]:X2}, 实际 0x{frame[i]:X2}";
            }
        }

        if (_trailerBytes.Length > 0)
        {
            if (frame.Length < _trailerBytes.Length)
                return $"帧长度不足以包含帧尾: 需要 {_trailerBytes.Length} 字节";

            int trailerStart = frame.Length - _trailerBytes.Length;
            for (int i = 0; i < _trailerBytes.Length; i++)
            {
                if (frame[trailerStart + i] != _trailerBytes[i])
                    return $"帧尾错误: 位置 {trailerStart + i} 期望 0x{_trailerBytes[i]:X2}, 实际 0x{frame[trailerStart + i]:X2}";
            }
        }

        return null;
    }

    public bool TryExtractFrame(byte[] buffer, out byte[] frame, out int consumedBytes)
    {
        frame = [];
        consumedBytes = 0;

        if (buffer == null || buffer.Length == 0)
            return false;

        int headerIndex = FindHeader(buffer);
        if (headerIndex < 0)
        {
            consumedBytes = Math.Max(0, buffer.Length - _headerBytes.Length + 1);
            return false;
        }

        int frameLength = DetermineFrameLength(buffer, headerIndex);
        if (frameLength <= 0)
            return false;

        if (headerIndex + frameLength > buffer.Length)
            return false;

        frame = new byte[frameLength];
        Array.Copy(buffer, headerIndex, frame, 0, frameLength);
        consumedBytes = headerIndex + frameLength;

        return Validate(frame);
    }

    private int FindHeader(byte[] buffer)
    {
        if (_headerBytes.Length == 0)
            return 0;

        for (int i = 0; i <= buffer.Length - _headerBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < _headerBytes.Length; j++)
            {
                if (buffer[i + j] != _headerBytes[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }

    private int DetermineFrameLength(byte[] buffer, int headerIndex)
    {
        if (Definition.LengthFieldConfig != null)
        {
            var config = Definition.LengthFieldConfig;
            int lengthFieldStart = headerIndex + config.StartIndex;

            if (lengthFieldStart + config.Length > buffer.Length)
                return -1;

            int length = ReadInteger(buffer, lengthFieldStart, config.Length, config.Endianness);
            int frameLength = length + config.Offset;

            if (!config.IncludesHeader)
                frameLength += _headerBytes.Length;
            if (!config.IncludesLengthField)
                frameLength += config.Length;

            return frameLength;
        }

        if (_trailerBytes.Length > 0)
        {
            for (int i = headerIndex + _headerBytes.Length; i <= buffer.Length - _trailerBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < _trailerBytes.Length; j++)
                {
                    if (buffer[i + j] != _trailerBytes[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i + _trailerBytes.Length - headerIndex;
            }
            return -1;
        }

        // 无长度字段且无帧尾时，使用 MinFrameLength 或字段定义所需的最大长度
        int requiredByFields = 0;
        if (Definition.Fields.Count > 0)
        {
            requiredByFields = Definition.Fields
                .Where(f => f.IsEnabled)
                .Select(f => _headerBytes.Length + f.StartIndex + (f.Length > 0 ? f.Length : FieldDefinition.GetDefaultLength(f.DataType)))
                .DefaultIfEmpty(0)
                .Max();
        }

        int minLen = Math.Max(Definition.MinFrameLength, requiredByFields);
        return minLen > 0 ? minLen : -1;
    }

    private bool ValidateChecksum(byte[] frame)
    {
        var config = Definition.ChecksumConfig!;

        int checksumStart = config.StartIndex >= 0
            ? config.StartIndex
            : frame.Length + config.StartIndex;

        if (checksumStart < 0 || checksumStart + config.Length > frame.Length)
            return false;

        int calcStart = config.CalculateStartIndex;
        int calcEnd = config.CalculateEndIndex >= 0
            ? config.CalculateEndIndex
            : frame.Length + config.CalculateEndIndex;

        if (calcStart < 0 || calcEnd > frame.Length || calcStart >= calcEnd)
            return false;

        byte[] dataToCheck = new byte[calcEnd - calcStart];
        Array.Copy(frame, calcStart, dataToCheck, 0, dataToCheck.Length);

        byte[] calculatedChecksum = _checksumService.Calculate(config.Algorithm, dataToCheck);

        byte[] frameChecksum = new byte[config.Length];
        Array.Copy(frame, checksumStart, frameChecksum, 0, config.Length);

        if (config.Endianness == Endianness.LittleEndian && calculatedChecksum.Length > 1)
        {
            Array.Reverse(calculatedChecksum);
        }

        if (calculatedChecksum.Length != frameChecksum.Length)
        {
            int minLen = Math.Min(calculatedChecksum.Length, frameChecksum.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (calculatedChecksum[calculatedChecksum.Length - minLen + i] != frameChecksum[frameChecksum.Length - minLen + i])
                    return false;
            }
            return true;
        }

        return calculatedChecksum.SequenceEqual(frameChecksum);
    }

    private ParsedField? ParseField(byte[] frame, FieldDefinition fieldDef)
    {
        // StartIndex is relative to data area (after header)
        int startIndex = fieldDef.StartIndex + _headerBytes.Length;
        int length = fieldDef.Length > 0
            ? fieldDef.Length
            : FieldDefinition.GetDefaultLength(fieldDef.DataType);

        if (length <= 0 || startIndex < _headerBytes.Length || startIndex + length > frame.Length)
            return null;

        byte[] rawBytes = new byte[length];
        Array.Copy(frame, startIndex, rawBytes, 0, length);

        var parsedField = new ParsedField
        {
            Name = fieldDef.Name,
            Description = fieldDef.Description,
            RawBytes = rawBytes,
            DataType = fieldDef.DataType,
            StartIndex = startIndex,
            Length = length,
            Value = ConvertToValue(rawBytes, fieldDef.DataType, fieldDef.Endianness)
        };

        foreach (var bitFieldDef in fieldDef.BitFields.Where(bf => bf.IsEnabled))
        {
            var parsedBitField = ParseBitField(rawBytes, bitFieldDef);
            if (parsedBitField != null)
            {
                parsedField.BitFields.Add(parsedBitField);
            }
        }

        return parsedField;
    }

    private ParsedBitField? ParseBitField(byte[] bytes, BitFieldDefinition bitFieldDef)
    {
        if (bytes.Length == 0)
            return null;

        ulong value;

        if (bitFieldDef.Mask.HasValue)
        {
            value = (ulong)(bytes[0] & bitFieldDef.Mask.Value);
            int shift = 0;
            byte mask = bitFieldDef.Mask.Value;
            while ((mask & 1) == 0 && shift < 8)
            {
                mask >>= 1;
                shift++;
            }
            value >>= shift;
        }
        else
        {
            ulong fullValue = 0;
            for (int i = 0; i < bytes.Length && i < 8; i++)
            {
                fullValue |= (ulong)bytes[i] << (i * 8);
            }

            ulong bitMask = ((1UL << bitFieldDef.BitCount) - 1) << bitFieldDef.BitOffset;
            value = (fullValue & bitMask) >> bitFieldDef.BitOffset;
        }

        return new ParsedBitField
        {
            Name = bitFieldDef.Name,
            Description = bitFieldDef.Description,
            Value = value,
            BitOffset = bitFieldDef.BitOffset,
            BitCount = bitFieldDef.BitCount
        };
    }

    private static object? ConvertToValue(byte[] bytes, DataType dataType, Endianness endianness)
    {
        if (bytes.Length == 0)
            return null;

        byte[] orderedBytes = endianness == Endianness.BigEndian
            ? bytes.Reverse().ToArray()
            : bytes;

        return dataType switch
        {
            DataType.UInt8 => bytes[0],
            DataType.Int8 => (sbyte)bytes[0],
            DataType.UInt16 when orderedBytes.Length >= 2 => BitConverter.ToUInt16(orderedBytes, 0),
            DataType.Int16 when orderedBytes.Length >= 2 => BitConverter.ToInt16(orderedBytes, 0),
            DataType.UInt32 when orderedBytes.Length >= 4 => BitConverter.ToUInt32(orderedBytes, 0),
            DataType.Int32 when orderedBytes.Length >= 4 => BitConverter.ToInt32(orderedBytes, 0),
            DataType.UInt64 when orderedBytes.Length >= 8 => BitConverter.ToUInt64(orderedBytes, 0),
            DataType.Int64 when orderedBytes.Length >= 8 => BitConverter.ToInt64(orderedBytes, 0),
            DataType.Float when orderedBytes.Length >= 4 => BitConverter.ToSingle(orderedBytes, 0),
            DataType.Double when orderedBytes.Length >= 8 => BitConverter.ToDouble(orderedBytes, 0),
            DataType.Bool => bytes[0] != 0,
            DataType.AsciiString => System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0'),
            DataType.Bytes => bytes,
            _ => bytes
        };
    }

    private static int ReadInteger(byte[] buffer, int offset, int length, Endianness endianness)
    {
        int value = 0;
        if (endianness == Endianness.BigEndian)
        {
            for (int i = 0; i < length; i++)
            {
                value = (value << 8) | buffer[offset + i];
            }
        }
        else
        {
            for (int i = length - 1; i >= 0; i--)
            {
                value = (value << 8) | buffer[offset + i];
            }
        }
        return value;
    }

    private static byte[] ParseHexString(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return [];

        return HexHelper.HexStringToBytes(hex);
    }

    public byte[] BuildFrame(Dictionary<string, object> fieldValues)
    {
        ArgumentNullException.ThrowIfNull(fieldValues);

        // Calculate frame size
        int frameSize = CalculateFrameSize(fieldValues);
        var frame = new byte[frameSize];

        // Fill header
        if (_headerBytes.Length > 0)
        {
            Array.Copy(_headerBytes, 0, frame, 0, _headerBytes.Length);
        }

        // Fill trailer
        if (_trailerBytes.Length > 0)
        {
            Array.Copy(_trailerBytes, 0, frame, frameSize - _trailerBytes.Length, _trailerBytes.Length);
        }

        // Fill field values
        int headerLen = _headerBytes.Length;
        int trailerStart = _trailerBytes.Length > 0 ? frameSize - _trailerBytes.Length : frameSize;
        foreach (var fieldDef in Definition.Fields.Where(f => f.IsEnabled))
        {
            if (!fieldValues.TryGetValue(fieldDef.Name, out var value))
                continue;

            int length = fieldDef.Length > 0
                ? fieldDef.Length
                : FieldDefinition.GetDefaultLength(fieldDef.DataType);

            // Offset StartIndex by header length to avoid overwriting header
            int writeOffset = fieldDef.StartIndex + headerLen;

            if (length <= 0 || writeOffset < headerLen || writeOffset + length > trailerStart)
                continue;

            var bytes = ConvertValueToBytes(value, fieldDef.DataType, fieldDef.Endianness, length);
            if (bytes.Length > 0)
            {
                Array.Copy(bytes, 0, frame, writeOffset, Math.Min(bytes.Length, length));
            }
        }

        // Calculate and fill checksum
        if (Definition.ChecksumConfig != null)
        {
            FillChecksum(frame);
        }

        return frame;
    }

    private int CalculateFrameSize(Dictionary<string, object> fieldValues)
    {
        int maxEnd = 0;

        // Consider header
        maxEnd = Math.Max(maxEnd, _headerBytes.Length);

        // Consider fields (StartIndex is relative to after header)
        int headerLen = _headerBytes.Length;
        foreach (var fieldDef in Definition.Fields.Where(f => f.IsEnabled))
        {
            int length = fieldDef.Length > 0
                ? fieldDef.Length
                : FieldDefinition.GetDefaultLength(fieldDef.DataType);
            maxEnd = Math.Max(maxEnd, headerLen + fieldDef.StartIndex + length);
        }

        // Consider checksum
        if (Definition.ChecksumConfig != null)
        {
            var config = Definition.ChecksumConfig;
            if (config.StartIndex >= 0)
            {
                maxEnd = Math.Max(maxEnd, config.StartIndex + config.Length);
            }
            else
            {
                // Negative index - checksum is at the end, we need to figure out the size
                // The frame size needs to accommodate the checksum at the end
                maxEnd = Math.Max(maxEnd, maxEnd + config.Length);
            }
        }

        // Consider trailer
        if (_trailerBytes.Length > 0)
        {
            maxEnd += _trailerBytes.Length;
        }

        return Math.Max(maxEnd, Definition.MinFrameLength);
    }

    private void FillChecksum(byte[] frame)
    {
        var config = Definition.ChecksumConfig!;

        int checksumStart = config.StartIndex >= 0
            ? config.StartIndex
            : frame.Length + config.StartIndex;

        int calcStart = config.CalculateStartIndex;
        int calcEnd = config.CalculateEndIndex >= 0
            ? config.CalculateEndIndex
            : frame.Length + config.CalculateEndIndex;

        if (calcStart < 0 || calcEnd > frame.Length || calcStart >= calcEnd)
            return;

        byte[] dataToCheck = new byte[calcEnd - calcStart];
        Array.Copy(frame, calcStart, dataToCheck, 0, dataToCheck.Length);

        byte[] calculatedChecksum = _checksumService.Calculate(config.Algorithm, dataToCheck);

        if (config.Endianness == Endianness.LittleEndian && calculatedChecksum.Length > 1)
        {
            Array.Reverse(calculatedChecksum);
        }

        // Copy checksum to frame (take last N bytes if checksum is longer)
        int copyLen = Math.Min(config.Length, calculatedChecksum.Length);
        int srcOffset = calculatedChecksum.Length - copyLen;
        if (checksumStart >= 0 && checksumStart + copyLen <= frame.Length)
        {
            Array.Copy(calculatedChecksum, srcOffset, frame, checksumStart, copyLen);
        }
    }

    private static byte[] ConvertValueToBytes(object value, DataType dataType, Endianness endianness, int targetLength)
    {
        byte[] rawBytes = ConvertToRawBytes(value, dataType, targetLength);

        if (rawBytes.Length == 0)
            return rawBytes;

        // Apply endianness for multi-byte types
        if (endianness == Endianness.BigEndian && rawBytes.Length > 1 && dataType != DataType.Bytes && dataType != DataType.AsciiString)
        {
            Array.Reverse(rawBytes);
        }

        return rawBytes;
    }

    private static byte[] ConvertToRawBytes(object value, DataType dataType, int targetLength)
    {
        // Handle string input (from UI)
        if (value is string strValue)
        {
            return ConvertStringToBytes(strValue, dataType, targetLength);
        }

        // Handle raw byte array (from Hex mode input)
        if (value is byte[] rawBytes)
        {
            return rawBytes;
        }

        return dataType switch
        {
            DataType.UInt8 => [Convert.ToByte(value)],
            DataType.Int8 => [unchecked((byte)Convert.ToSByte(value))],
            DataType.UInt16 => BitConverter.GetBytes(Convert.ToUInt16(value)),
            DataType.Int16 => BitConverter.GetBytes(Convert.ToInt16(value)),
            DataType.UInt32 => BitConverter.GetBytes(Convert.ToUInt32(value)),
            DataType.Int32 => BitConverter.GetBytes(Convert.ToInt32(value)),
            DataType.UInt64 => BitConverter.GetBytes(Convert.ToUInt64(value)),
            DataType.Int64 => BitConverter.GetBytes(Convert.ToInt64(value)),
            DataType.Float => BitConverter.GetBytes(Convert.ToSingle(value)),
            DataType.Double => BitConverter.GetBytes(Convert.ToDouble(value)),
            DataType.Bool => [(byte)(Convert.ToBoolean(value) ? 1 : 0)],
            DataType.AsciiString => System.Text.Encoding.ASCII.GetBytes(value.ToString() ?? string.Empty),
            DataType.Bytes when value is byte[] byteArr => byteArr,
            _ => []
        };
    }

    public List<FieldInputItem> GetBuildFieldInputs()
    {
        var inputs = new List<FieldInputItem>();

        foreach (var field in Definition.Fields.Where(f => f.IsEnabled))
        {
            inputs.Add(new FieldInputItem
            {
                FieldName = field.Name,
                DisplayName = field.Name,
                Description = field.Description,
                DataType = field.DataType,
                DefaultValue = string.Empty,
                IsHexMode = field.DataType is DataType.Bytes or DataType.UInt8
            });
        }

        return inputs;
    }

    private static byte[] ConvertStringToBytes(string strValue, DataType dataType, int targetLength)
    {
        return dataType switch
        {
            DataType.UInt8 => [byte.Parse(strValue)],
            DataType.Int8 => [unchecked((byte)sbyte.Parse(strValue))],
            DataType.UInt16 => BitConverter.GetBytes(ushort.Parse(strValue)),
            DataType.Int16 => BitConverter.GetBytes(short.Parse(strValue)),
            DataType.UInt32 => BitConverter.GetBytes(uint.Parse(strValue)),
            DataType.Int32 => BitConverter.GetBytes(int.Parse(strValue)),
            DataType.UInt64 => BitConverter.GetBytes(ulong.Parse(strValue)),
            DataType.Int64 => BitConverter.GetBytes(long.Parse(strValue)),
            DataType.Float => BitConverter.GetBytes(float.Parse(strValue)),
            DataType.Double => BitConverter.GetBytes(double.Parse(strValue)),
            DataType.Bool => [(byte)(bool.Parse(strValue) ? 1 : 0)],
            DataType.AsciiString => System.Text.Encoding.ASCII.GetBytes(strValue),
            DataType.Bytes => HexHelper.HexStringToBytes(strValue),
            _ => []
        };
    }
}
