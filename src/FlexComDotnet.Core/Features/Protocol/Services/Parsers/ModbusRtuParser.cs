using System.Text;
using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Models.ModbusRtu;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.Protocol.Services.Parsers;

/// <summary>
/// Modbus-RTU 协议解析器
/// </summary>
public class ModbusRtuParser : IProtocolParser
{
    private const int CrcLength = 2;
    private const int MinFrameLength = 5; // SlaveID(1) + FC(1) + ExceptionCode(1) + CRC(2)
    private const int RequestHeaderLength = 2; // SlaveID + FC
    private const int RegisterSize = 2; // 每个寄存器 2 字节

    private readonly IChecksumService _checksumService;
    private readonly ModbusRtuConfig _config;

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public FrameDefinition Definition { get; }

    public ModbusRtuParser(FrameDefinition definition, IChecksumService checksumService)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _config = definition.ModbusRtuConfig ?? new ModbusRtuConfig();
    }

    public ParsedFrame Parse(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var result = new ModbusRtuParsedFrame
        {
            RawData = frame,
            ProtocolName = Name
        };

        if (frame.Length < MinFrameLength)
        {
            result.IsValid = false;
            result.ErrorMessage = $"帧长度不足: 最小 {MinFrameLength} 字节, 实际 {frame.Length} 字节";
            return result;
        }

        if (!ValidateCrc(frame))
        {
            result.IsValid = false;
            result.ChecksumValid = false;
            result.ErrorMessage = "CRC-16/MODBUS 校验失败";
            return result;
        }

        result.ChecksumValid = true;
        result.SlaveId = frame[0];
        result.FunctionCodeRaw = frame[1];

        // 添加基础字段
        AddBasicFields(result, frame);

        // 检测异常响应 (功能码最高位为1)
        if (ModbusFunctionCodeExtensions.IsExceptionResponse(frame[1]))
        {
            ParseExceptionResponse(result, frame);
            result.IsResponseFrame = true;
        }
        else
        {
            result.FunctionCode = (ModbusFunctionCode)frame[1];
            ParseNormalResponse(result, frame);
            DetectFrameDirection(result, frame);
        }

        // 在基础字段后插入帧方向字段
        var directionLabel = result.IsResponseFrame ? "响应" : "请求";
        result.Fields.Insert(1, new ParsedField
        {
            Name = "帧方向",
            Description = directionLabel,
            RawBytes = [],
            Value = directionLabel,
            DataType = DataType.AsciiString,
            StartIndex = -1,
            Length = 0
        });

        result.IsValid = true;
        return result;
    }

    public bool Validate(byte[] frame)
    {
        if (frame == null || frame.Length < MinFrameLength)
            return false;

        return ValidateCrc(frame);
    }

    public bool TryExtractFrame(byte[] buffer, out byte[] frame, out int consumedBytes)
    {
        frame = [];
        consumedBytes = 0;

        if (buffer == null || buffer.Length < MinFrameLength)
            return false;

        // Modbus-RTU 没有固定帧头标识，需要根据 SlaveId 和 FC 来尝试定位
        for (int startIndex = 0; startIndex <= buffer.Length - MinFrameLength; startIndex++)
        {
            byte possibleSlaveId = buffer[startIndex];
            byte possibleFc = buffer[startIndex + 1];

            // 从站地址范围 1-247, 功能码高位为1表示异常
            if (possibleSlaveId == 0 || possibleSlaveId > 247)
                continue;

            int expectedLength = CalculateExpectedFrameLength(buffer, startIndex);
            if (expectedLength < MinFrameLength)
                continue;

            if (startIndex + expectedLength > buffer.Length)
            {
                // 数据不够，等待更多数据
                consumedBytes = startIndex;
                return false;
            }

            var candidate = new byte[expectedLength];
            Array.Copy(buffer, startIndex, candidate, 0, expectedLength);

            if (ValidateCrc(candidate))
            {
                frame = candidate;
                consumedBytes = startIndex + expectedLength;
                return true;
            }
        }

        consumedBytes = Math.Max(0, buffer.Length - MinFrameLength + 1);
        return false;
    }

    public List<FieldInputItem> GetBuildFieldInputs()
    {
        var inputs = new List<FieldInputItem>
        {
            new()
            {
                FieldName = "从站地址",
                DisplayName = "从站地址",
                Description = "Slave ID (1-247)",
                DataType = DataType.UInt8,
                DefaultValue = _config.SlaveId.ToString()
            },
            new()
            {
                FieldName = "帧方向",
                DisplayName = "帧方向",
                Description = "请求 或 响应",
                DataType = DataType.AsciiString,
                DefaultValue = "请求",
                Value = "请求",
                IsToggleMode = true,
                ToggleTrueLabel = "响应",
                ToggleFalseLabel = "请求"
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

        // 获取从站地址
        byte slaveId = GetByteValue(fieldValues, "从站地址", _config.SlaveId);
        byte functionCode = (byte)_config.FunctionCode;

        // 判断帧方向: "请求" or "响应"
        bool isResponse = IsResponseDirection(fieldValues);

        byte[] frameWithoutCrc;

        if (isResponse)
        {
            frameWithoutCrc = _config.FunctionCode switch
            {
                ModbusFunctionCode.ReadHoldingRegisters or
                ModbusFunctionCode.ReadInputRegisters => BuildReadResponse(slaveId, functionCode, fieldValues),

                ModbusFunctionCode.WriteSingleRegister => BuildWriteSingleRegisterResponse(slaveId, fieldValues),

                ModbusFunctionCode.WriteMultipleRegisters => BuildWriteMultipleRegistersResponse(slaveId),

                _ => throw new NotSupportedException($"不支持的功能码: 0x{functionCode:X2}")
            };
        }
        else
        {
            frameWithoutCrc = _config.FunctionCode switch
            {
                ModbusFunctionCode.ReadHoldingRegisters or
                ModbusFunctionCode.ReadInputRegisters => BuildReadRequest(slaveId, functionCode),

                ModbusFunctionCode.WriteSingleRegister => BuildWriteSingleRegisterRequest(slaveId, fieldValues),

                ModbusFunctionCode.WriteMultipleRegisters => BuildWriteMultipleRegistersRequest(slaveId, fieldValues),

                _ => throw new NotSupportedException($"不支持的功能码: 0x{functionCode:X2}")
            };
        }

        // 追加 CRC
        return AppendCrc(frameWithoutCrc);
    }

    private static bool IsResponseDirection(Dictionary<string, object> fieldValues)
    {
        if (!fieldValues.TryGetValue("帧方向", out var direction))
            return false;

        var dirStr = direction?.ToString() ?? "";
        return dirStr is "响应" or "response" or "Response";
    }

    #region 解析辅助方法

    private void AddBasicFields(ModbusRtuParsedFrame result, byte[] frame)
    {
        result.Fields.Add(new ParsedField
        {
            Name = "从站地址",
            Description = $"Slave ID: {frame[0]}",
            RawBytes = [frame[0]],
            Value = frame[0],
            DataType = DataType.UInt8,
            StartIndex = 0,
            Length = 1
        });

        result.Fields.Add(new ParsedField
        {
            Name = "功能码",
            Description = GetFunctionCodeDescription(frame[1]),
            RawBytes = [frame[1]],
            Value = frame[1],
            DataType = DataType.UInt8,
            StartIndex = 1,
            Length = 1
        });

        // CRC 字段
        result.Fields.Add(new ParsedField
        {
            Name = "CRC校验",
            Description = "CRC-16/MODBUS",
            RawBytes = [frame[^2], frame[^1]],
            Value = (ushort)(frame[^2] | (frame[^1] << 8)),
            DataType = DataType.UInt16,
            StartIndex = frame.Length - CrcLength,
            Length = CrcLength
        });
    }

    private static string GetFunctionCodeDescription(byte fc)
    {
        if (ModbusFunctionCodeExtensions.IsExceptionResponse(fc))
        {
            byte originalFc = (byte)(fc & 0x7F);
            return $"异常响应 (原功能码: 0x{originalFc:X2})";
        }

        return ModbusFunctionCodeExtensions.IsValid(fc)
            ? ((ModbusFunctionCode)fc).GetDescription()
            : $"未知功能码 (0x{fc:X2})";
    }

    private void ParseExceptionResponse(ModbusRtuParsedFrame result, byte[] frame)
    {
        result.IsExceptionResponse = true;
        result.FunctionCode = (ModbusFunctionCode)(frame[1] & 0x7F);

        if (frame.Length >= MinFrameLength)
        {
            result.ExceptionCode = frame[2];
            result.ExceptionDescription = ModbusFunctionCodeExtensions.GetExceptionDescription(frame[2]);

            result.Fields.Add(new ParsedField
            {
                Name = "异常码",
                Description = result.ExceptionDescription,
                RawBytes = [frame[2]],
                Value = frame[2],
                DataType = DataType.UInt8,
                StartIndex = 2,
                Length = 1
            });
        }
    }

    private void ParseNormalResponse(ModbusRtuParsedFrame result, byte[] frame)
    {
        switch (result.FunctionCode)
        {
            case ModbusFunctionCode.ReadHoldingRegisters:
            case ModbusFunctionCode.ReadInputRegisters:
                ParseReadResponse(result, frame);
                break;

            case ModbusFunctionCode.WriteSingleRegister:
                ParseWriteSingleRegister(result, frame);
                break;

            case ModbusFunctionCode.WriteMultipleRegisters:
                ParseWriteMultipleRegisters(result, frame);
                break;
        }
    }

    /// <summary>
    /// 解析读响应 (FC 03/04):
    /// 请求: [SlaveID][FC][StartAddr Hi][StartAddr Lo][Qty Hi][Qty Lo][CRC]
    /// 响应: [SlaveID][FC][ByteCount][Data...][CRC]
    /// </summary>
    private void ParseReadResponse(ModbusRtuParsedFrame result, byte[] frame)
    {
        int dataLength = frame.Length - RequestHeaderLength - CrcLength;

        if (dataLength >= 1 && IsReadResponse(frame))
        {
            // 响应帧: [SlaveID][FC][ByteCount][Data...][CRC]
            byte byteCount = frame[2];
            result.ByteCount = byteCount;

            result.Fields.Add(new ParsedField
            {
                Name = "字节数",
                Description = $"{byteCount} 字节数据",
                RawBytes = [frame[2]],
                Value = byteCount,
                DataType = DataType.UInt8,
                StartIndex = 2,
                Length = 1
            });

            if (byteCount > 0 && frame.Length >= 3 + byteCount + CrcLength)
            {
                result.RegisterData = new byte[byteCount];
                Array.Copy(frame, 3, result.RegisterData, 0, byteCount);

                // 解析用户自定义数据项
                ParseCustomDataFields(result, result.RegisterData, 3);
            }
        }
        else if (dataLength >= 4)
        {
            // 请求帧: [SlaveID][FC][StartAddr Hi][StartAddr Lo][Qty Hi][Qty Lo][CRC]
            result.StartAddress = (ushort)((frame[2] << 8) | frame[3]);
            result.Quantity = (ushort)((frame[4] << 8) | frame[5]);

            result.Fields.Add(new ParsedField
            {
                Name = "起始地址",
                Description = $"0x{result.StartAddress:X4} ({result.StartAddress})",
                RawBytes = [frame[2], frame[3]],
                Value = result.StartAddress,
                DataType = DataType.UInt16,
                StartIndex = 2,
                Length = 2
            });

            result.Fields.Add(new ParsedField
            {
                Name = "寄存器数量",
                Description = $"{result.Quantity} 个寄存器",
                RawBytes = [frame[4], frame[5]],
                Value = result.Quantity,
                DataType = DataType.UInt16,
                StartIndex = 4,
                Length = 2
            });
        }
    }

    /// <summary>
    /// 检测帧方向（请求/响应）
    /// </summary>
    private static void DetectFrameDirection(ModbusRtuParsedFrame result, byte[] frame)
    {
        switch (result.FunctionCode)
        {
            case ModbusFunctionCode.ReadHoldingRegisters:
            case ModbusFunctionCode.ReadInputRegisters:
                result.IsResponseFrame = IsReadResponse(frame);
                break;
            case ModbusFunctionCode.WriteSingleRegister:
                // FC06 请求和响应格式相同，无法区分
                result.IsResponseFrame = false;
                break;
            case ModbusFunctionCode.WriteMultipleRegisters:
                // FC10 请求帧包含 ByteCount + Data，响应帧固定 8 字节
                result.IsResponseFrame = frame.Length == 6 + CrcLength;
                break;
        }
    }

    /// <summary>
    /// 判断是否为读响应帧 (通过 ByteCount 特征判断)
    /// 响应帧第3字节是 ByteCount，请求帧是 StartAddress Hi
    /// </summary>
    private static bool IsReadResponse(byte[] frame)
    {
        if (frame.Length < 5)
            return false;

        byte possibleByteCount = frame[2];
        int expectedResponseLength = 3 + possibleByteCount + CrcLength;

        // 如果按照 ByteCount 解释时帧长度匹配，则认为是响应帧
        return frame.Length == expectedResponseLength && possibleByteCount > 0;
    }

    /// <summary>
    /// 解析写单个寄存器 (FC 06):
    /// 请求/响应格式相同: [SlaveID][FC][RegAddr Hi][RegAddr Lo][Value Hi][Value Lo][CRC]
    /// </summary>
    private void ParseWriteSingleRegister(ModbusRtuParsedFrame result, byte[] frame)
    {
        if (frame.Length < 6 + CrcLength) return;

        result.StartAddress = (ushort)((frame[2] << 8) | frame[3]);
        ushort writeValue = (ushort)((frame[4] << 8) | frame[5]);

        result.Fields.Add(new ParsedField
        {
            Name = "寄存器地址",
            Description = $"0x{result.StartAddress:X4} ({result.StartAddress})",
            RawBytes = [frame[2], frame[3]],
            Value = result.StartAddress,
            DataType = DataType.UInt16,
            StartIndex = 2,
            Length = 2
        });

        result.Fields.Add(new ParsedField
        {
            Name = "写入值",
            Description = $"0x{writeValue:X4} ({writeValue})",
            RawBytes = [frame[4], frame[5]],
            Value = writeValue,
            DataType = DataType.UInt16,
            StartIndex = 4,
            Length = 2
        });

        result.RegisterData = [frame[4], frame[5]];

        // 解析用户自定义数据项 (对于 FC06，数据从偏移4开始)
        ParseCustomDataFields(result, result.RegisterData, 4);
    }

    /// <summary>
    /// 解析写多个寄存器 (FC 10):
    /// 请求: [SlaveID][FC][StartAddr Hi][StartAddr Lo][Qty Hi][Qty Lo][ByteCount][Data...][CRC]
    /// 响应: [SlaveID][FC][StartAddr Hi][StartAddr Lo][Qty Hi][Qty Lo][CRC]
    /// </summary>
    private void ParseWriteMultipleRegisters(ModbusRtuParsedFrame result, byte[] frame)
    {
        if (frame.Length < 6 + CrcLength) return;

        result.StartAddress = (ushort)((frame[2] << 8) | frame[3]);
        result.Quantity = (ushort)((frame[4] << 8) | frame[5]);

        result.Fields.Add(new ParsedField
        {
            Name = "起始地址",
            Description = $"0x{result.StartAddress:X4} ({result.StartAddress})",
            RawBytes = [frame[2], frame[3]],
            Value = result.StartAddress,
            DataType = DataType.UInt16,
            StartIndex = 2,
            Length = 2
        });

        result.Fields.Add(new ParsedField
        {
            Name = "寄存器数量",
            Description = $"{result.Quantity} 个寄存器",
            RawBytes = [frame[4], frame[5]],
            Value = result.Quantity,
            DataType = DataType.UInt16,
            StartIndex = 4,
            Length = 2
        });

        // 检查是否为请求帧 (包含 ByteCount + Data)
        if (frame.Length > 8 + CrcLength)
        {
            byte byteCount = frame[6];
            result.ByteCount = byteCount;

            result.Fields.Add(new ParsedField
            {
                Name = "字节数",
                Description = $"{byteCount} 字节数据",
                RawBytes = [frame[6]],
                Value = byteCount,
                DataType = DataType.UInt8,
                StartIndex = 6,
                Length = 1
            });

            if (byteCount > 0 && frame.Length >= 7 + byteCount + CrcLength)
            {
                result.RegisterData = new byte[byteCount];
                Array.Copy(frame, 7, result.RegisterData, 0, byteCount);

                // 解析用户自定义数据项
                ParseCustomDataFields(result, result.RegisterData, 7);
            }
        }
    }

    /// <summary>
    /// 解析用户自定义的数据项字段
    /// </summary>
    private void ParseCustomDataFields(ModbusRtuParsedFrame result, byte[] data, int dataStartInFrame)
    {
        var customFields = Definition.Fields.Where(f => f.IsEnabled).ToList();
        if (customFields.Count == 0 || data.Length == 0)
            return;

        foreach (var fieldDef in customFields.OrderBy(f => f.StartIndex))
        {
            int length = fieldDef.Length > 0
                ? fieldDef.Length
                : FieldDefinition.GetDefaultLength(fieldDef.DataType);

            if (length <= 0 || fieldDef.StartIndex < 0 || fieldDef.StartIndex + length > data.Length)
                continue;

            byte[] rawBytes = new byte[length];
            Array.Copy(data, fieldDef.StartIndex, rawBytes, 0, length);

            var parsedField = new ParsedField
            {
                Name = fieldDef.Name,
                Description = fieldDef.Description,
                RawBytes = rawBytes,
                DataType = fieldDef.DataType,
                StartIndex = dataStartInFrame + fieldDef.StartIndex,
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
        }
    }

    #endregion

    #region 组帧辅助方法

    private byte[] BuildReadRequest(byte slaveId, byte functionCode)
    {
        ushort startAddress = _config.StartAddress;
        ushort quantity = _config.Quantity;

        return
        [
            slaveId,
            functionCode,
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),
            (byte)(quantity >> 8), (byte)(quantity & 0xFF)
        ];
    }

    /// <summary>
    /// 构建读响应帧: [SlaveID][FC][ByteCount][RegisterData...][CRC]
    /// </summary>
    private byte[] BuildReadResponse(byte slaveId, byte functionCode, Dictionary<string, object> fieldValues)
    {
        ushort quantity = _config.Quantity;
        byte[] dataBytes = BuildDataFromFields(fieldValues, quantity);
        byte byteCount = (byte)dataBytes.Length;

        var frame = new byte[3 + byteCount];
        frame[0] = slaveId;
        frame[1] = functionCode;
        frame[2] = byteCount;
        Array.Copy(dataBytes, 0, frame, 3, byteCount);

        return frame;
    }

    private byte[] BuildWriteSingleRegisterRequest(byte slaveId, Dictionary<string, object> fieldValues)
    {
        ushort registerAddress = _config.StartAddress;

        // 获取写入值: 优先从自定义字段获取，否则从 fieldValues 中查找
        byte[] valueBytes;
        var customFields = Definition.Fields.Where(f => f.IsEnabled).ToList();
        if (customFields.Count > 0 && fieldValues.TryGetValue(customFields[0].Name, out var fieldVal))
        {
            valueBytes = ConvertFieldValueToBytes(fieldVal, customFields[0].DataType, customFields[0].Endianness, RegisterSize);
        }
        else if (fieldValues.TryGetValue("写入值", out var writeVal))
        {
            valueBytes = ConvertFieldValueToBytes(writeVal, DataType.UInt16, Endianness.BigEndian, RegisterSize);
        }
        else
        {
            valueBytes = [0x00, 0x00];
        }

        // 确保写入值恰好2字节
        var padded = new byte[RegisterSize];
        Array.Copy(valueBytes, 0, padded, 0, Math.Min(valueBytes.Length, RegisterSize));

        return
        [
            slaveId,
            (byte)ModbusFunctionCode.WriteSingleRegister,
            (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
            padded[0], padded[1]
        ];
    }

    /// <summary>
    /// 构建写单个寄存器响应帧 (回显): [SlaveID][FC][RegAddr][Value][CRC]
    /// </summary>
    private byte[] BuildWriteSingleRegisterResponse(byte slaveId, Dictionary<string, object> fieldValues)
    {
        // FC06 响应与请求格式相同
        return BuildWriteSingleRegisterRequest(slaveId, fieldValues);
    }

    private byte[] BuildWriteMultipleRegistersRequest(byte slaveId, Dictionary<string, object> fieldValues)
    {
        ushort startAddress = _config.StartAddress;
        ushort quantity = _config.Quantity;

        // 构建数据域
        byte[] dataBytes = BuildDataFromFields(fieldValues, quantity);
        byte byteCount = (byte)dataBytes.Length;

        var frame = new byte[7 + byteCount];
        frame[0] = slaveId;
        frame[1] = (byte)ModbusFunctionCode.WriteMultipleRegisters;
        frame[2] = (byte)(startAddress >> 8);
        frame[3] = (byte)(startAddress & 0xFF);
        frame[4] = (byte)(quantity >> 8);
        frame[5] = (byte)(quantity & 0xFF);
        frame[6] = byteCount;
        Array.Copy(dataBytes, 0, frame, 7, byteCount);

        return frame;
    }

    /// <summary>
    /// 构建写多个寄存器响应帧: [SlaveID][FC][StartAddr][Quantity][CRC]
    /// </summary>
    private byte[] BuildWriteMultipleRegistersResponse(byte slaveId)
    {
        ushort startAddress = _config.StartAddress;
        ushort quantity = _config.Quantity;

        return
        [
            slaveId,
            (byte)ModbusFunctionCode.WriteMultipleRegisters,
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),
            (byte)(quantity >> 8), (byte)(quantity & 0xFF)
        ];
    }

    private byte[] BuildDataFromFields(Dictionary<string, object> fieldValues, ushort quantity)
    {
        int totalBytes = quantity * RegisterSize;
        var data = new byte[totalBytes];

        var customFields = Definition.Fields.Where(f => f.IsEnabled).ToList();
        if (customFields.Count == 0)
            return data;

        foreach (var fieldDef in customFields)
        {
            if (!fieldValues.TryGetValue(fieldDef.Name, out var value))
                continue;

            int length = fieldDef.Length > 0
                ? fieldDef.Length
                : FieldDefinition.GetDefaultLength(fieldDef.DataType);

            if (length <= 0 || fieldDef.StartIndex < 0 || fieldDef.StartIndex + length > totalBytes)
                continue;

            var bytes = ConvertFieldValueToBytes(value, fieldDef.DataType, fieldDef.Endianness, length);
            Array.Copy(bytes, 0, data, fieldDef.StartIndex, Math.Min(bytes.Length, length));
        }

        return data;
    }

    #endregion

    #region CRC 计算

    private bool ValidateCrc(byte[] frame)
    {
        if (frame.Length < MinFrameLength)
            return false;

        byte[] dataWithoutCrc = new byte[frame.Length - CrcLength];
        Array.Copy(frame, dataWithoutCrc, dataWithoutCrc.Length);

        byte[] calculatedCrc = _checksumService.Calculate(ChecksumAlgorithmType.Crc16Modbus, dataWithoutCrc);

        // CRC-16/MODBUS: 低字节在前，高字节在后
        return frame[^2] == calculatedCrc[^1] && frame[^1] == calculatedCrc[^2];
    }

    private byte[] AppendCrc(byte[] data)
    {
        var crc = _checksumService.Calculate(ChecksumAlgorithmType.Crc16Modbus, data);
        var result = new byte[data.Length + CrcLength];
        Array.Copy(data, result, data.Length);
        result[data.Length] = crc[^1];       // CRC Lo
        result[data.Length + 1] = crc[^2];   // CRC Hi
        return result;
    }

    #endregion

    #region 值转换辅助

    private int CalculateExpectedFrameLength(byte[] buffer, int startIndex)
    {
        if (startIndex + 2 > buffer.Length)
            return -1;

        byte fc = buffer[startIndex + 1];

        // 异常响应: SlaveID + FC + ExceptionCode + CRC = 5 bytes
        if (ModbusFunctionCodeExtensions.IsExceptionResponse(fc))
            return 5;

        byte baseFc = (byte)(fc & 0x7F);

        switch (baseFc)
        {
            case 0x03:
            case 0x04:
                // 需要判断是请求还是响应
                // 请求帧: 8 bytes (SlaveID + FC + StartAddr(2) + Qty(2) + CRC(2))
                // 响应帧: 3 + ByteCount + CRC(2)
                if (startIndex + 3 > buffer.Length)
                    return -1;

                byte possibleByteCount = buffer[startIndex + 2];
                int responseLength = 3 + possibleByteCount + CrcLength;
                int requestLength = 8;

                // 优先尝试响应格式
                if (startIndex + responseLength <= buffer.Length && possibleByteCount > 0 && possibleByteCount % 2 == 0)
                    return responseLength;
                if (startIndex + requestLength <= buffer.Length)
                    return requestLength;

                return -1;

            case 0x06:
                return 8; // SlaveID + FC + RegAddr(2) + Value(2) + CRC(2)

            case 0x10:
                // 请求: 7 + ByteCount + CRC(2)
                // 响应: 8 bytes  
                if (startIndex + 7 > buffer.Length)
                    return -1;

                if (buffer.Length >= startIndex + 7)
                {
                    byte bc = buffer[startIndex + 6];
                    int fc10RequestLength = 7 + bc + CrcLength;
                    // 如果按请求帧解释有效，使用请求长度
                    if (startIndex + fc10RequestLength <= buffer.Length && bc > 0)
                        return fc10RequestLength;
                }
                return 8; // 响应

            default:
                return -1;
        }
    }

    private static byte GetByteValue(Dictionary<string, object> fieldValues, string key, byte defaultValue)
    {
        if (!fieldValues.TryGetValue(key, out var value))
            return defaultValue;

        if (value is byte b) return b;
        if (value is byte[] bytes && bytes.Length > 0) return bytes[0];
        if (value is string s && byte.TryParse(s, out byte parsed)) return parsed;

        return defaultValue;
    }

    private static ushort GetUshortValue(Dictionary<string, object> fieldValues, string key, ushort defaultValue)
    {
        if (!fieldValues.TryGetValue(key, out var value))
            return defaultValue;

        if (value is ushort u) return u;
        if (value is string s && ushort.TryParse(s, out ushort parsed)) return parsed;
        if (value is int i) return (ushort)i;
        if (value is byte[] bytes && bytes.Length >= 2) return (ushort)((bytes[0] << 8) | bytes[1]);

        return defaultValue;
    }

    private static byte[] ConvertFieldValueToBytes(object value, DataType dataType, Endianness endianness, int targetLength)
    {
        byte[] rawBytes;

        if (value is byte[] byteInput)
            return byteInput;

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
                DataType.Bytes => HexHelper.HexStringToBytes(strValue),
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

        // Apply endianness for multi-byte numeric types
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

    #endregion
}
