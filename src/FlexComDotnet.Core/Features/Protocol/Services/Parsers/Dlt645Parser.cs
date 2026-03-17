using System.Text;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Models.Dlt645;

namespace FlexComDotnet.Core.Features.Protocol.Services.Parsers;

/// <summary>
/// DL/T 645-2007 协议解析器
/// </summary>
public class Dlt645Parser : IProtocolParser
{
    private const byte FrameStart = 0x68;
    private const byte FrameEnd = 0x16;
    private const byte WakeupByte = 0xFE;
    private const int MinFrameLength = 12;
    private const int AddressLength = 6;
    private const int DataIdentifierLength = 4;

    private readonly FrameDefinition? _customDefinition;

    public string Name => _customDefinition?.Name ?? "DL/T 645-2007";
    public string Description => _customDefinition?.Description ?? "中国电力行业标准多功能电能表通信协议";

    public FrameDefinition Definition { get; }

    /// <summary>
    /// 创建默认的 DL/T 645-2007 解析器
    /// </summary>
    public Dlt645Parser() : this(null) { }

    /// <summary>
    /// 创建带自定义字段定义的 DL/T 645-2007 解析器
    /// </summary>
    /// <param name="customDefinition">自定义协议定义，其中 Fields 定义数据域内的子字段（索引相对于数据域起始位置）</param>
    public Dlt645Parser(FrameDefinition? customDefinition)
    {
        _customDefinition = customDefinition;
        Definition = customDefinition ?? new FrameDefinition
        {
            Name = "DL/T 645-2007",
            Description = "中国电力行业标准多功能电能表通信协议",
            Header = "68",
            Trailer = "16",
            MinFrameLength = MinFrameLength,
            MaxFrameLength = 256
        };
    }

    public ParsedFrame Parse(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var result = new Dlt645ParsedFrame
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

        if (!ValidateChecksum(frame))
        {
            result.IsValid = false;
            result.ChecksumValid = false;
            byte calculatedCs = 0;
            for (int i = 0; i < frame.Length - 2; i++)
                calculatedCs += frame[i];
            result.ErrorMessage = $"校验和错误: 计算值 0x{calculatedCs:X2}, 帧中值 0x{frame[^2]:X2}";
            return result;
        }

        result.ChecksumValid = true;
        result.MeterAddress = ParseAddress(frame, 1);
        result.ControlCode = new Dlt645ControlCode(frame[8]);
        result.DataLength = frame[9];

        if (result.DataLength > 0)
        {
            var dataField = new byte[result.DataLength];
            Array.Copy(frame, 10, dataField, 0, result.DataLength);
            result.DecodedDataField = DecodeDataField(dataField);
        }

        AddBasicFields(result, frame);

        if (result.DataLength > 0)
        {
            if (result.ControlCode.IsError)
            {
                ParseErrorResponse(result);
            }
            else
            {
                ParseDataResponse(result);
            }
        }

        result.IsValid = true;
        return result;
    }

    public bool Validate(byte[] frame)
    {
        return ValidateWithReason(frame) == null;
    }

    private static string? ValidateWithReason(byte[] frame)
    {
        if (frame == null || frame.Length < MinFrameLength)
            return $"帧长度不足: 最小 {MinFrameLength} 字节, 实际 {frame?.Length ?? 0} 字节";

        if (frame[0] != FrameStart)
            return $"帧起始符错误: 期望 0x68, 实际 0x{frame[0]:X2}";

        if (frame[7] != FrameStart)
            return $"第二帧起始符错误: 期望 0x68, 实际 0x{frame[7]:X2}";

        if (frame[^1] != FrameEnd)
            return $"帧结束符错误: 期望 0x16, 实际 0x{frame[^1]:X2}";

        int dataLength = frame[9];
        int expectedLength = 12 + dataLength;
        if (frame.Length != expectedLength)
            return $"帧长度不匹配: 数据域长度 {dataLength}, 期望总长 {expectedLength}, 实际 {frame.Length}";

        return null;
    }

    public bool TryExtractFrame(byte[] buffer, out byte[] frame, out int consumedBytes)
    {
        frame = [];
        consumedBytes = 0;

        if (buffer == null || buffer.Length < MinFrameLength)
            return false;

        int startIndex = FindFrameStart(buffer);
        if (startIndex < 0)
        {
            consumedBytes = Math.Max(0, buffer.Length - 1);
            return false;
        }

        consumedBytes = startIndex;

        int remaining = buffer.Length - startIndex;
        if (remaining < MinFrameLength)
            return false;

        if (buffer[startIndex + 7] != FrameStart)
        {
            consumedBytes = startIndex + 1;
            return false;
        }

        int dataLength = buffer[startIndex + 9];
        int frameLength = 12 + dataLength;

        if (remaining < frameLength)
            return false;

        frame = new byte[frameLength];
        Array.Copy(buffer, startIndex, frame, 0, frameLength);
        consumedBytes = startIndex + frameLength;

        if (!Validate(frame) || !ValidateChecksum(frame))
        {
            frame = [];
            consumedBytes = startIndex + 1;
            return false;
        }

        return true;
    }

    private static int FindFrameStart(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == WakeupByte)
                continue;

            if (buffer[i] == FrameStart)
                return i;
        }
        return -1;
    }

    private static bool ValidateChecksum(byte[] frame)
    {
        int checksumIndex = frame.Length - 2;
        byte expectedCs = frame[checksumIndex];

        byte calculatedCs = 0;
        for (int i = 0; i < checksumIndex; i++)
        {
            calculatedCs += frame[i];
        }

        return calculatedCs == expectedCs;
    }

    private static string ParseAddress(byte[] frame, int startIndex)
    {
        var sb = new StringBuilder(12);
        for (int i = startIndex + AddressLength - 1; i >= startIndex; i--)
        {
            sb.Append(frame[i].ToString("X2"));
        }
        return sb.ToString();
    }

    private static byte[] DecodeDataField(byte[] encodedData)
    {
        var decoded = new byte[encodedData.Length];
        for (int i = 0; i < encodedData.Length; i++)
        {
            decoded[i] = (byte)(encodedData[i] - 0x33);
        }
        return decoded;
    }

    private void AddBasicFields(Dlt645ParsedFrame result, byte[] frame)
    {
        result.Fields.Add(new ParsedField
        {
            Name = "帧起始符",
            Description = "固定值 0x68",
            RawBytes = [frame[0]],
            Value = frame[0],
            DataType = DataType.UInt8,
            StartIndex = 0,
            Length = 1
        });

        var addressBytes = new byte[AddressLength];
        Array.Copy(frame, 1, addressBytes, 0, AddressLength);
        result.Fields.Add(new ParsedField
        {
            Name = "电表地址",
            Description = "6字节BCD码，低字节在前",
            RawBytes = addressBytes,
            Value = result.MeterAddress,
            DataType = DataType.Bytes,
            StartIndex = 1,
            Length = AddressLength
        });

        result.Fields.Add(new ParsedField
        {
            Name = "控制码",
            Description = result.ControlCode?.ToString() ?? "",
            RawBytes = [frame[8]],
            Value = frame[8],
            DataType = DataType.UInt8,
            StartIndex = 8,
            Length = 1
        });

        result.Fields.Add(new ParsedField
        {
            Name = "数据域长度",
            Description = $"{result.DataLength} 字节",
            RawBytes = [frame[9]],
            Value = result.DataLength,
            DataType = DataType.UInt8,
            StartIndex = 9,
            Length = 1
        });

        if (result.DataLength > 0)
        {
            var dataFieldBytes = new byte[result.DataLength];
            Array.Copy(frame, 10, dataFieldBytes, 0, result.DataLength);
            result.Fields.Add(new ParsedField
            {
                Name = "数据域",
                Description = "已加33H编码",
                RawBytes = dataFieldBytes,
                Value = result.DecodedDataField,
                DataType = DataType.Bytes,
                StartIndex = 10,
                Length = result.DataLength
            });

            // 解析用户自定义的数据域子字段（相对于解码后的数据域）
            ParseCustomDataFields(result);
        }

        result.Fields.Add(new ParsedField
        {
            Name = "校验码",
            Description = "算术累加和",
            RawBytes = [frame[^2]],
            Value = frame[^2],
            DataType = DataType.UInt8,
            StartIndex = frame.Length - 2,
            Length = 1
        });

        result.Fields.Add(new ParsedField
        {
            Name = "帧结束符",
            Description = "固定值 0x16",
            RawBytes = [frame[^1]],
            Value = frame[^1],
            DataType = DataType.UInt8,
            StartIndex = frame.Length - 1,
            Length = 1
        });
    }

    private void ParseErrorResponse(Dlt645ParsedFrame result)
    {
        if (result.DecodedDataField.Length < 1)
            return;

        result.ErrorByte = result.DecodedDataField[0];
        result.ErrorDescriptions = Dlt645ErrorCodeExtensions.GetAllErrors(result.ErrorByte.Value);
        result.ErrorMessage = string.Join(", ", result.ErrorDescriptions);

        result.Fields.Add(new ParsedField
        {
            Name = "错误信息字",
            Description = result.ErrorMessage,
            RawBytes = [result.ErrorByte.Value],
            Value = result.ErrorByte.Value,
            DataType = DataType.UInt8,
            StartIndex = 10,
            Length = 1
        });
    }

    private void ParseDataResponse(Dlt645ParsedFrame result)
    {
        if (result.DecodedDataField.Length < DataIdentifierLength)
            return;

        byte di0 = result.DecodedDataField[0];
        byte di1 = result.DecodedDataField[1];
        byte di2 = result.DecodedDataField[2];
        byte di3 = result.DecodedDataField[3];

        result.DataIdentifier = (uint)(di3 << 24 | di2 << 16 | di1 << 8 | di0);
        result.DataIdentifierInfo = Dlt645DataDictionary.GetIdentifier(result.DataIdentifier.Value);

        var diBytes = new byte[DataIdentifierLength];
        Array.Copy(result.DecodedDataField, 0, diBytes, 0, DataIdentifierLength);
        result.Fields.Add(new ParsedField
        {
            Name = "数据标识",
            Description = result.DataIdentifierInfo?.Name ?? $"未知(0x{result.DataIdentifier:X8})",
            RawBytes = diBytes,
            Value = result.DataIdentifier,
            DataType = DataType.UInt32,
            StartIndex = 10,
            Length = DataIdentifierLength
        });

        if (result.DecodedDataField.Length > DataIdentifierLength)
        {
            var dataBytes = new byte[result.DecodedDataField.Length - DataIdentifierLength];
            Array.Copy(result.DecodedDataField, DataIdentifierLength, dataBytes, 0, dataBytes.Length);

            var (value, formatted) = ParseDataValue(dataBytes, result.DataIdentifierInfo);
            result.DataValue = value;
            result.FormattedValue = formatted;

            result.Fields.Add(new ParsedField
            {
                Name = result.DataIdentifierInfo?.Name ?? "数据值",
                Description = formatted,
                RawBytes = dataBytes,
                Value = value,
                DataType = DataType.Bytes,
                StartIndex = 10 + DataIdentifierLength,
                Length = dataBytes.Length
            });
        }
    }

    private static (object? value, string formatted) ParseDataValue(byte[] data, Dlt645DataIdentifier? identifier)
    {
        if (data.Length == 0)
            return (null, "");

        if (identifier == null)
        {
            return (data, BitConverter.ToString(data).Replace("-", " "));
        }

        return identifier.Format switch
        {
            Dlt645DataFormat.BcdUnsigned => ParseBcdUnsigned(data, identifier.DecimalPlaces, identifier.Unit),
            Dlt645DataFormat.BcdSigned => ParseBcdSigned(data, identifier.DecimalPlaces, identifier.Unit),
            Dlt645DataFormat.Ascii => ParseAscii(data),
            Dlt645DataFormat.DateTime => ParseDateTime(data),
            Dlt645DataFormat.Date => ParseDate(data),
            Dlt645DataFormat.Time => ParseTime(data),
            _ => (data, BitConverter.ToString(data).Replace("-", " "))
        };
    }

    private static (decimal value, string formatted) ParseBcdUnsigned(byte[] data, int decimalPlaces, string unit)
    {
        long intValue = 0;
        for (int i = data.Length - 1; i >= 0; i--)
        {
            int high = (data[i] >> 4) & 0x0F;
            int low = data[i] & 0x0F;
            intValue = intValue * 100 + high * 10 + low;
        }

        decimal value = intValue / (decimal)Math.Pow(10, decimalPlaces);
        string format = "F" + decimalPlaces;
        string formatted = string.IsNullOrEmpty(unit) ? value.ToString(format) : $"{value.ToString(format)} {unit}";
        return (value, formatted);
    }

    private static (decimal value, string formatted) ParseBcdSigned(byte[] data, int decimalPlaces, string unit)
    {
        if (data.Length == 0)
            return (0, "0");

        bool isNegative = (data[^1] & 0x80) != 0;
        var workData = (byte[])data.Clone();
        workData[^1] = (byte)(workData[^1] & 0x7F);

        var (absValue, _) = ParseBcdUnsigned(workData, decimalPlaces, "");
        decimal value = isNegative ? -absValue : absValue;
        string format = "F" + decimalPlaces;
        string formatted = string.IsNullOrEmpty(unit) ? value.ToString(format) : $"{value.ToString(format)} {unit}";
        return (value, formatted);
    }

    private static (string value, string formatted) ParseAscii(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = data.Length - 1; i >= 0; i--)
        {
            sb.Append(data[i].ToString("X2"));
        }
        string value = sb.ToString();
        return (value, value);
    }

    private static (DateTime? value, string formatted) ParseDateTime(byte[] data)
    {
        if (data.Length < 4)
            return (null, "无效日期时间");

        try
        {
            int year = 2000 + BcdToByte(data[3]);
            int month = BcdToByte(data[2]);
            int day = BcdToByte((byte)(data[1] & 0x3F));
            int weekDay = (data[1] >> 5) & 0x07;

            var date = new DateTime(year, month, day);
            string formatted = $"{date:yyyy-MM-dd} 星期{GetWeekDayName(weekDay)}";
            return (date, formatted);
        }
        catch
        {
            return (null, "无效日期时间");
        }
    }

    private static (DateTime? value, string formatted) ParseDate(byte[] data)
    {
        if (data.Length < 3)
            return (null, "无效日期");

        try
        {
            int year = 2000 + BcdToByte(data[2]);
            int month = BcdToByte(data[1]);
            int day = BcdToByte(data[0]);

            var date = new DateTime(year, month, day);
            return (date, date.ToString("yyyy-MM-dd"));
        }
        catch
        {
            return (null, "无效日期");
        }
    }

    private static (TimeSpan? value, string formatted) ParseTime(byte[] data)
    {
        if (data.Length < 3)
            return (null, "无效时间");

        try
        {
            int second = BcdToByte(data[0]);
            int minute = BcdToByte(data[1]);
            int hour = BcdToByte(data[2]);

            var time = new TimeSpan(hour, minute, second);
            return (time, time.ToString(@"hh\:mm\:ss"));
        }
        catch
        {
            return (null, "无效时间");
        }
    }

    private static int BcdToByte(byte bcd)
    {
        return ((bcd >> 4) & 0x0F) * 10 + (bcd & 0x0F);
    }

    private static string GetWeekDayName(int weekDay) => weekDay switch
    {
        1 => "一",
        2 => "二",
        3 => "三",
        4 => "四",
        5 => "五",
        6 => "六",
        7 or 0 => "日",
        _ => "?"
    };

    /// <summary>
    /// 解析用户自定义的数据域子字段
    /// 字段索引相对于解码后的数据域（已减33H）
    /// </summary>
    private void ParseCustomDataFields(Dlt645ParsedFrame result)
    {
        var customFields = _customDefinition?.Fields.Where(f => f.IsEnabled).ToList();
        if (customFields == null || customFields.Count == 0)
            return;

        var decodedData = result.DecodedDataField;
        if (decodedData.Length == 0)
            return;

        // 自定义字段的 StartIndex 相对于数据标识之后的数据内容区域
        int fieldOffset = DataIdentifierLength;

        // 跟踪已解析的字节索引
        var parsedIndices = new HashSet<int>();

        foreach (var fieldDef in customFields.OrderBy(f => f.StartIndex))
        {
            int length = fieldDef.Length > 0
                ? fieldDef.Length
                : FieldDefinition.GetDefaultLength(fieldDef.DataType);

            int actualStart = fieldOffset + fieldDef.StartIndex;

            if (length <= 0 || actualStart < 0 || actualStart + length > decodedData.Length)
                continue;

            byte[] rawBytes = new byte[length];
            Array.Copy(decodedData, actualStart, rawBytes, 0, length);

            var parsedField = new ParsedField
            {
                Name = fieldDef.Name,
                Description = fieldDef.Description,
                RawBytes = rawBytes,
                DataType = fieldDef.DataType,
                // StartIndex 相对于整个帧（数据域起始位置10 + 数据域内偏移）
                StartIndex = 10 + actualStart,
                Length = length,
                Value = ConvertToValue(rawBytes, fieldDef.DataType, fieldDef.Endianness)
            };

            // 解析位域
            foreach (var bitFieldDef in fieldDef.BitFields.Where(bf => bf.IsEnabled))
            {
                var parsedBitField = ParseBitField(rawBytes, bitFieldDef);
                if (parsedBitField != null)
                {
                    parsedField.BitFields.Add(parsedBitField);
                }
            }

            result.Fields.Add(parsedField);

            // 记录已解析的索引
            for (int i = actualStart; i < actualStart + length; i++)
            {
                parsedIndices.Add(i);
            }
        }

        // 标记数据标识区域为已解析（由 ParseDataResponse 处理）
        for (int i = 0; i < Math.Min(DataIdentifierLength, decodedData.Length); i++)
        {
            parsedIndices.Add(i);
        }

        // 添加剩余数据字段（未被自定义字段覆盖的部分）
        AddRemainingDataField(result, decodedData, parsedIndices);
    }

    /// <summary>
    /// 添加剩余数据字段（未被自定义字段解析的部分）
    /// </summary>
    private static void AddRemainingDataField(Dlt645ParsedFrame result, byte[] decodedData, HashSet<int> parsedIndices)
    {
        var remainingBytes = new List<byte>();
        var remainingIndices = new List<int>();

        for (int i = 0; i < decodedData.Length; i++)
        {
            if (!parsedIndices.Contains(i))
            {
                remainingBytes.Add(decodedData[i]);
                if (remainingIndices.Count == 0 || remainingIndices[^1] != i - 1)
                {
                    remainingIndices.Add(i);
                }
            }
        }

        if (remainingBytes.Count > 0)
        {
            int firstUnparsedIndex = 0;
            for (int i = 0; i < decodedData.Length; i++)
            {
                if (!parsedIndices.Contains(i))
                {
                    firstUnparsedIndex = i;
                    break;
                }
            }

            result.Fields.Add(new ParsedField
            {
                Name = "剩余数据",
                Description = $"未定义的 {remainingBytes.Count} 字节",
                RawBytes = [.. remainingBytes],
                Value = remainingBytes.ToArray(),
                DataType = DataType.Bytes,
                StartIndex = 10 + firstUnparsedIndex,
                Length = remainingBytes.Count
            });
        }
    }

    private static ParsedBitField? ParseBitField(byte[] bytes, BitFieldDefinition bitFieldDef)
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

    public List<FieldInputItem> GetBuildFieldInputs()
    {
        var inputs = new List<FieldInputItem>
        {
            new()
            {
                FieldName = "电表地址",
                DisplayName = "电表地址",
                Description = "12位BCD码地址",
                DataType = DataType.AsciiString,
                DefaultValue = "000000000000"
            },
            new()
            {
                FieldName = "控制码",
                DisplayName = "控制码",
                Description = "功能控制字节 (Hex)",
                DataType = DataType.UInt8,
                DefaultValue = "11"
            },
            new()
            {
                FieldName = "数据标识",
                DisplayName = "数据标识",
                Description = "4字节数据标识 (十进制)",
                DataType = DataType.UInt32,
                DefaultValue = "65536"
            }
        };

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

    public byte[] BuildFrame(Dictionary<string, object> fieldValues)
    {
        ArgumentNullException.ThrowIfNull(fieldValues);

        // Parse address (6 bytes BCD, little-endian)
        byte[] addressBytes;
        if (fieldValues.TryGetValue("电表地址", out var addrObj))
        {
            if (addrObj is byte[] addrBytes)
            {
                // Hex mode: raw bytes, pad/truncate to 6 bytes (already reversed by user)
                addressBytes = new byte[AddressLength];
                Array.Copy(addrBytes, 0, addressBytes, 0, Math.Min(addrBytes.Length, AddressLength));
            }
            else
            {
                addressBytes = ParseAddressToBytes(addrObj.ToString() ?? "000000000000");
            }
        }
        else
        {
            addressBytes = ParseAddressToBytes("000000000000");
        }

        // Parse control code
        byte controlCode;
        if (fieldValues.TryGetValue("控制码", out var ccObj))
        {
            if (ccObj is byte[] ccBytes && ccBytes.Length > 0)
                controlCode = ccBytes[0];
            else
                controlCode = Convert.ToByte(ccObj);
        }
        else
        {
            controlCode = 0x11; // Default: read data
        }

        // Build data field
        byte[] dataField = BuildDataField(fieldValues);

        // Encode data field (+33H)
        byte[] encodedData = EncodeDataField(dataField);

        // Build frame: 68 + addr(6) + 68 + ctrl + len + data + cs + 16
        int frameLength = 12 + encodedData.Length;
        var frame = new byte[frameLength];

        frame[0] = FrameStart;
        Array.Copy(addressBytes, 0, frame, 1, AddressLength);
        frame[7] = FrameStart;
        frame[8] = controlCode;
        frame[9] = (byte)encodedData.Length;
        Array.Copy(encodedData, 0, frame, 10, encodedData.Length);

        // Calculate checksum
        byte cs = 0;
        for (int i = 0; i < frameLength - 2; i++)
            cs += frame[i];
        frame[frameLength - 2] = cs;
        frame[frameLength - 1] = FrameEnd;

        return frame;
    }

    private byte[] BuildDataField(Dictionary<string, object> fieldValues)
    {
        var dataBytes = new List<byte>();

        // Data identifier (4 bytes, little-endian)
        if (fieldValues.TryGetValue("数据标识", out var diObj))
        {
            if (diObj is byte[] diBytes)
            {
                // Hex mode: raw bytes, pad/truncate to 4
                for (int i = 0; i < 4; i++)
                    dataBytes.Add(i < diBytes.Length ? diBytes[i] : (byte)0);
            }
            else
            {
                uint di = Convert.ToUInt32(diObj);
                dataBytes.Add((byte)(di & 0xFF));
                dataBytes.Add((byte)((di >> 8) & 0xFF));
                dataBytes.Add((byte)((di >> 16) & 0xFF));
                dataBytes.Add((byte)((di >> 24) & 0xFF));
            }
        }

        // Custom fields (user-defined data domain sub-fields)
        if (_customDefinition?.Fields.Any(f => f.IsEnabled) == true)
        {
            // Custom field StartIndex is relative to data content (after data identifier)
            int fieldOffset = dataBytes.Count; // After data identifier (4 bytes)

            // Determine max data size needed
            int maxEnd = fieldOffset;
            foreach (var fieldDef in _customDefinition.Fields.Where(f => f.IsEnabled))
            {
                int length = fieldDef.Length > 0
                    ? fieldDef.Length
                    : FieldDefinition.GetDefaultLength(fieldDef.DataType);
                maxEnd = Math.Max(maxEnd, fieldOffset + fieldDef.StartIndex + length);
            }

            // Extend data bytes if needed
            while (dataBytes.Count < maxEnd)
                dataBytes.Add(0);

            // Fill custom field values
            foreach (var fieldDef in _customDefinition.Fields.Where(f => f.IsEnabled))
            {
                if (!fieldValues.TryGetValue(fieldDef.Name, out var value))
                    continue;

                int length = fieldDef.Length > 0
                    ? fieldDef.Length
                    : FieldDefinition.GetDefaultLength(fieldDef.DataType);

                var bytes = ConvertFieldValueToBytes(value, fieldDef.DataType, fieldDef.Endianness, length);
                for (int i = 0; i < Math.Min(bytes.Length, length) && fieldOffset + fieldDef.StartIndex + i < dataBytes.Count; i++)
                {
                    dataBytes[fieldOffset + fieldDef.StartIndex + i] = bytes[i];
                }
            }
        }
        // Additional raw data
        else if (fieldValues.TryGetValue("数据值", out var dataVal))
        {
            if (dataVal is byte[] rawData)
            {
                dataBytes.AddRange(rawData);
            }
            else if (dataVal is string hexStr)
            {
                var parsed = Serial.Helpers.HexHelper.HexStringToBytes(hexStr);
                dataBytes.AddRange(parsed);
            }
        }

        return [.. dataBytes];
    }

    private static byte[] EncodeDataField(byte[] data)
    {
        var encoded = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            encoded[i] = (byte)(data[i] + 0x33);
        }
        return encoded;
    }

    private static byte[] ParseAddressToBytes(string address)
    {
        var bytes = new byte[AddressLength];
        // Remove non-hex characters
        address = address.Replace(" ", "").Replace("-", "");

        // Pad to 12 characters
        address = address.PadLeft(12, '0');
        if (address.Length > 12) address = address[..12];

        // Convert to BCD bytes in reverse order (little-endian)
        for (int i = 0; i < AddressLength; i++)
        {
            int strIndex = (AddressLength - 1 - i) * 2;
            if (strIndex + 2 <= address.Length)
            {
                byte.TryParse(address.AsSpan(strIndex, 2),
                    System.Globalization.NumberStyles.HexNumber, null, out bytes[i]);
            }
        }

        return bytes;
    }

    private static byte[] ConvertFieldValueToBytes(object value, DataType dataType, Endianness endianness, int targetLength)
    {
        byte[] rawBytes;

        // When value is already byte[] (e.g., Hex mode input), use directly
        if (value is byte[] byteInput)
        {
            return byteInput;
        }

        if (value is string strValue)
        {
            rawBytes = dataType switch
            {
                DataType.UInt8 => [byte.Parse(strValue)],
                DataType.Int8 => [unchecked((byte)sbyte.Parse(strValue))],
                DataType.UInt16 => BitConverter.GetBytes(ushort.Parse(strValue)),
                DataType.Int16 => BitConverter.GetBytes(short.Parse(strValue)),
                DataType.UInt32 => BitConverter.GetBytes(uint.Parse(strValue)),
                DataType.Int32 => BitConverter.GetBytes(int.Parse(strValue)),
                DataType.Float => BitConverter.GetBytes(float.Parse(strValue)),
                DataType.Double => BitConverter.GetBytes(double.Parse(strValue)),
                DataType.Bool => [(byte)(bool.Parse(strValue) ? 1 : 0)],
                DataType.AsciiString => Encoding.ASCII.GetBytes(strValue),
                DataType.Bytes => Serial.Helpers.HexHelper.HexStringToBytes(strValue),
                _ => []
            };
        }
        else
        {
            rawBytes = dataType switch
            {
                DataType.UInt8 => [Convert.ToByte(value)],
                DataType.Int8 => [unchecked((byte)Convert.ToSByte(value))],
                DataType.UInt16 => BitConverter.GetBytes(Convert.ToUInt16(value)),
                DataType.Int16 => BitConverter.GetBytes(Convert.ToInt16(value)),
                DataType.UInt32 => BitConverter.GetBytes(Convert.ToUInt32(value)),
                DataType.Int32 => BitConverter.GetBytes(Convert.ToInt32(value)),
                DataType.Float => BitConverter.GetBytes(Convert.ToSingle(value)),
                DataType.Double => BitConverter.GetBytes(Convert.ToDouble(value)),
                DataType.Bool => [(byte)(Convert.ToBoolean(value) ? 1 : 0)],
                DataType.AsciiString => Encoding.ASCII.GetBytes(value.ToString() ?? ""),
                DataType.Bytes when value is byte[] byteArr => byteArr,
                _ => []
            };
        }

        // Apply endianness for multi-byte non-string types
        if (endianness == Endianness.BigEndian && rawBytes.Length > 1 && dataType != DataType.Bytes && dataType != DataType.AsciiString)
        {
            Array.Reverse(rawBytes);
        }

        return rawBytes;
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
            DataType.AsciiString => Encoding.ASCII.GetString(bytes).TrimEnd('\0'),
            DataType.Bytes => bytes,
            _ => bytes
        };
    }
}
