using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Models.ModbusRtu;
using FlexComDotnet.Core.Features.Protocol.Services.Parsers;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Protocol;

public class ModbusRtuParserTests
{
    private readonly IChecksumService _checksumService = new ChecksumService();

    private ModbusRtuParser CreateParser(
        ModbusFunctionCode functionCode = ModbusFunctionCode.ReadHoldingRegisters,
        byte slaveId = 1,
        ushort startAddress = 0,
        ushort quantity = 1,
        List<FieldDefinition>? fields = null)
    {
        var definition = new FrameDefinition
        {
            Name = "TestModbus",
            Description = "Test Modbus-RTU Protocol",
            ProtocolType = ProtocolType.ModbusRtu,
            ModbusRtuConfig = new ModbusRtuConfig
            {
                SlaveId = slaveId,
                FunctionCode = functionCode,
                StartAddress = startAddress,
                Quantity = quantity
            },
            Fields = fields ?? []
        };
        return new ModbusRtuParser(definition, _checksumService);
    }

    /// <summary>
    /// 计算 CRC-16/MODBUS 并追加到数据末尾
    /// </summary>
    private byte[] AppendCrc(byte[] data)
    {
        var crc = _checksumService.Calculate(ChecksumAlgorithmType.Crc16Modbus, data);
        // CRC-16/MODBUS: low byte first, then high byte
        var result = new byte[data.Length + 2];
        Array.Copy(data, result, data.Length);
        result[data.Length] = crc[^1];       // CRC Lo
        result[data.Length + 1] = crc[^2];   // CRC Hi
        return result;
    }

    #region 基本属性测试

    [Fact]
    public void Name_ShouldReturnDefinitionName()
    {
        var parser = CreateParser();
        parser.Name.Should().Be("TestModbus");
    }

    [Fact]
    public void Description_ShouldReturnDefinitionDescription()
    {
        var parser = CreateParser();
        parser.Description.Should().Be("Test Modbus-RTU Protocol");
    }

    [Fact]
    public void Definition_ShouldReturnFrameDefinition()
    {
        var parser = CreateParser();
        parser.Definition.ProtocolType.Should().Be(ProtocolType.ModbusRtu);
    }

    #endregion

    #region FC 03 读保持寄存器 - 请求帧解析

    [Fact]
    public void Parse_FC03Request_ShouldExtractSlaveIdAndFunctionCode()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 2);

        // FC03 请求: [01] [03] [00 00] [00 02] [CRC]
        var frameData = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x02 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.ChecksumValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.SlaveId.Should().Be(1);
        modbusResult.FunctionCode.Should().Be(ModbusFunctionCode.ReadHoldingRegisters);
        modbusResult.IsExceptionResponse.Should().BeFalse();
    }

    [Fact]
    public void Parse_FC03Response_ShouldExtractRegisterData()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 2);

        // FC03 响应: [01] [03] [04] [00 0A] [00 14] [CRC]
        // 4 bytes data = 2 registers, values: 10, 20
        var frameData = new byte[] { 0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x14 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.ByteCount.Should().Be(4);
        modbusResult.RegisterData.Should().Equal(0x00, 0x0A, 0x00, 0x14);
    }

    [Fact]
    public void Parse_FC03Response_WithCustomFields_ShouldExtractDataItems()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "温度",
                Description = "温度值",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            },
            new()
            {
                Name = "湿度",
                Description = "湿度值",
                StartIndex = 2,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, quantity: 2, fields: fields);

        // FC03 响应: [01] [03] [04] [00 0A] [00 14] [CRC]
        var frameData = new byte[] { 0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x14 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var tempField = result.GetField("温度");
        tempField.Should().NotBeNull();
        tempField!.Value.Should().Be((ushort)10);

        var humField = result.GetField("湿度");
        humField.Should().NotBeNull();
        humField!.Value.Should().Be((ushort)20);
    }

    #endregion

    #region FC 04 读输入寄存器

    [Fact]
    public void Parse_FC04Response_ShouldExtractRegisterData()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadInputRegisters, slaveId: 2, startAddress: 100, quantity: 1);

        // FC04 响应: [02] [04] [02] [01 F4] [CRC] (value: 500)
        var frameData = new byte[] { 0x02, 0x04, 0x02, 0x01, 0xF4 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.SlaveId.Should().Be(2);
        modbusResult.FunctionCode.Should().Be(ModbusFunctionCode.ReadInputRegisters);
        modbusResult.ByteCount.Should().Be(2);
    }

    #endregion

    #region FC 06 写单个寄存器

    [Fact]
    public void Parse_FC06Request_ShouldExtractAddressAndValue()
    {
        var parser = CreateParser(ModbusFunctionCode.WriteSingleRegister, slaveId: 1, startAddress: 100);

        // FC06 请求/响应: [01] [06] [00 64] [00 0A] [CRC] 
        // Write value 10 to register 100
        var frameData = new byte[] { 0x01, 0x06, 0x00, 0x64, 0x00, 0x0A };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.FunctionCode.Should().Be(ModbusFunctionCode.WriteSingleRegister);
        modbusResult.StartAddress.Should().Be(100);
    }

    #endregion

    #region FC 10 写多个寄存器

    [Fact]
    public void Parse_FC10Request_ShouldExtractDataFields()
    {
        var parser = CreateParser(ModbusFunctionCode.WriteMultipleRegisters, slaveId: 1, startAddress: 0, quantity: 2);

        // FC10 请求: [01] [10] [00 00] [00 02] [04] [00 0A] [00 14] [CRC]
        var frameData = new byte[] { 0x01, 0x10, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00, 0x0A, 0x00, 0x14 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.FunctionCode.Should().Be(ModbusFunctionCode.WriteMultipleRegisters);
        modbusResult.StartAddress.Should().Be(0);
        modbusResult.Quantity.Should().Be(2);
    }

    [Fact]
    public void Parse_FC10Response_ShouldExtractStartAddressAndQuantity()
    {
        var parser = CreateParser(ModbusFunctionCode.WriteMultipleRegisters, slaveId: 1, startAddress: 0, quantity: 2);

        // FC10 响应: [01] [10] [00 00] [00 02] [CRC]
        var frameData = new byte[] { 0x01, 0x10, 0x00, 0x00, 0x00, 0x02 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.StartAddress.Should().Be(0);
        modbusResult.Quantity.Should().Be(2);
    }

    #endregion

    #region 异常响应

    [Fact]
    public void Parse_ExceptionResponse_ShouldDetectAndDescribe()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters);

        // 异常响应: [01] [83] [02] [CRC] (FC 03 error, illegal data address)
        var frameData = new byte[] { 0x01, 0x83, 0x02 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result.Should().BeOfType<ModbusRtuParsedFrame>().Subject;
        modbusResult.IsExceptionResponse.Should().BeTrue();
        modbusResult.ExceptionCode.Should().Be(0x02);
        modbusResult.ExceptionDescription.Should().Contain("非法数据地址");
    }

    #endregion

    #region CRC 校验

    [Fact]
    public void Parse_WithInvalidCrc_ShouldReportChecksumFailure()
    {
        var parser = CreateParser();

        // 故意将 CRC 设为错误值
        var frame = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0xFF, 0xFF };

        var result = parser.Parse(frame);

        result.IsValid.Should().BeFalse();
        result.ChecksumValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithValidCrc_ShouldReturnTrue()
    {
        var parser = CreateParser();

        var frameData = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 };
        var frame = AppendCrc(frameData);

        parser.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTooShortFrame_ShouldReturnFalse()
    {
        var parser = CreateParser();
        parser.Validate([0x01, 0x03]).Should().BeFalse();
    }

    #endregion

    #region TryExtractFrame 流提取

    [Fact]
    public void TryExtractFrame_WithCompleteFC03Response_ShouldExtract()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, quantity: 2);

        // FC03 响应: [01] [03] [04] [data x4] [CRC x2] = 9 bytes
        var frameData = new byte[] { 0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x14 };
        var frame = AppendCrc(frameData);

        var result = parser.TryExtractFrame(frame, out var extracted, out var consumed);

        result.Should().BeTrue();
        extracted.Should().Equal(frame);
        consumed.Should().Be(frame.Length);
    }

    [Fact]
    public void TryExtractFrame_WithIncompleteFrame_ShouldReturnFalse()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, quantity: 2);

        // 不完整的帧
        var buffer = new byte[] { 0x01, 0x03, 0x04, 0x00 };

        var result = parser.TryExtractFrame(buffer, out _, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryExtractFrame_WithLeadingGarbage_ShouldSkipAndExtract()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, quantity: 1);

        // FC03 响应: [01] [03] [02] [00 0A] [CRC]
        var frameData = new byte[] { 0x01, 0x03, 0x02, 0x00, 0x0A };
        var validFrame = AppendCrc(frameData);

        // 前面加两个垃圾字节
        var buffer = new byte[2 + validFrame.Length];
        buffer[0] = 0xFF;
        buffer[1] = 0xFE;
        Array.Copy(validFrame, 0, buffer, 2, validFrame.Length);

        // TryExtractFrame 应从 SlaveId 匹配的位置尝试提取
        var result = parser.TryExtractFrame(buffer, out var extracted, out var consumed);

        // 由于 SlaveId 不匹配前面的垃圾数据，应该能跳过
        result.Should().BeTrue();
        extracted.Should().Equal(validFrame);
    }

    #endregion

    #region BuildFrame 组帧

    [Fact]
    public void BuildFrame_FC03Request_ShouldBuildCorrectFrame()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 2);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "1"
        };

        var frame = parser.BuildFrame(fieldValues);

        // 验证帧内容: [01] [03] [00 00] [00 02] [CRC x2]
        frame.Length.Should().Be(8);
        frame[0].Should().Be(0x01); // Slave ID
        frame[1].Should().Be(0x03); // FC
        frame[2].Should().Be(0x00); // Start Addr Hi
        frame[3].Should().Be(0x00); // Start Addr Lo
        frame[4].Should().Be(0x00); // Quantity Hi
        frame[5].Should().Be(0x02); // Quantity Lo

        // 验证 CRC
        var crc = _checksumService.Calculate(ChecksumAlgorithmType.Crc16Modbus, frame[..6]);
        frame[6].Should().Be(crc[^1]); // CRC Lo
        frame[7].Should().Be(crc[^2]); // CRC Hi
    }

    [Fact]
    public void BuildFrame_FC03Response_ShouldBuildCorrectFrame()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "温度",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 1, fields: fields);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "1",
            ["帧方向"] = "响应",
            ["温度"] = "258"
        };

        var frame = parser.BuildFrame(fieldValues);

        // 响应帧: [01] [03] [02] [01 02] [CRC x2]
        frame.Length.Should().Be(7);
        frame[0].Should().Be(0x01); // Slave ID
        frame[1].Should().Be(0x03); // FC
        frame[2].Should().Be(0x02); // ByteCount (1 register = 2 bytes)
        frame[3].Should().Be(0x01); // Temp Hi (258 = 0x0102)
        frame[4].Should().Be(0x02); // Temp Lo
    }

    [Fact]
    public void BuildFrame_FC03Response_RoundTrip_ShouldParseback()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "温度",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.Int16,
                Endianness = Endianness.BigEndian
            },
            new()
            {
                Name = "湿度",
                StartIndex = 2,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 2, fields: fields);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "1",
            ["帧方向"] = "响应",
            ["温度"] = "-5",
            ["湿度"] = "65"
        };

        var frame = parser.BuildFrame(fieldValues);
        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.GetValue<short>("温度").Should().Be(-5);
        result.GetValue<ushort>("湿度").Should().Be(65);
    }

    [Fact]
    public void BuildFrame_FC06Request_ShouldBuildCorrectFrame()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "写入值",
                Description = "寄存器值",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.WriteSingleRegister, slaveId: 1, startAddress: 100, fields: fields);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "1",
            ["写入值"] = "500"
        };

        var frame = parser.BuildFrame(fieldValues);

        // [01] [06] [00 64] [01 F4] [CRC x2]
        frame.Length.Should().Be(8);
        frame[0].Should().Be(0x01);
        frame[1].Should().Be(0x06);
        frame[2].Should().Be(0x00);
        frame[3].Should().Be(0x64);
        frame[4].Should().Be(0x01);
        frame[5].Should().Be(0xF4);
    }

    [Fact]
    public void BuildFrame_FC10Request_ShouldBuildCorrectFrame()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "温度",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            },
            new()
            {
                Name = "湿度",
                StartIndex = 2,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.WriteMultipleRegisters, slaveId: 1, startAddress: 0, quantity: 2, fields: fields);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "1",
            ["温度"] = "10",
            ["湿度"] = "20"
        };

        var frame = parser.BuildFrame(fieldValues);

        // [01] [10] [00 00] [00 02] [04] [00 0A] [00 14] [CRC x2]
        frame.Length.Should().Be(13);
        frame[0].Should().Be(0x01); // Slave ID
        frame[1].Should().Be(0x10); // FC
        frame[2].Should().Be(0x00); // Start Addr Hi
        frame[3].Should().Be(0x00); // Start Addr Lo
        frame[4].Should().Be(0x00); // Quantity Hi
        frame[5].Should().Be(0x02); // Quantity Lo
        frame[6].Should().Be(0x04); // Byte Count
        frame[7].Should().Be(0x00); // Temp Hi
        frame[8].Should().Be(0x0A); // Temp Lo (10)
        frame[9].Should().Be(0x00); // Humi Hi
        frame[10].Should().Be(0x14); // Humi Lo (20)
    }

    [Fact]
    public void BuildFrame_FC10Response_ShouldBuildConfirmationFrame()
    {
        var parser = CreateParser(ModbusFunctionCode.WriteMultipleRegisters, slaveId: 2, startAddress: 10, quantity: 3);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "2",
            ["帧方向"] = "响应"
        };

        var frame = parser.BuildFrame(fieldValues);

        // 响应帧: [02] [10] [00 0A] [00 03] [CRC x2]
        frame.Length.Should().Be(8);
        frame[0].Should().Be(0x02); // Slave ID
        frame[1].Should().Be(0x10); // FC
        frame[2].Should().Be(0x00); // Start Addr Hi (10 = 0x000A)
        frame[3].Should().Be(0x0A); // Start Addr Lo
        frame[4].Should().Be(0x00); // Quantity Hi (3)
        frame[5].Should().Be(0x03); // Quantity Lo
    }

    [Fact]
    public void BuildFrame_ShouldAutoAppendCrc()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 1);

        var fieldValues = new Dictionary<string, object>
        {
            ["从站地址"] = "1"
        };

        var frame = parser.BuildFrame(fieldValues);

        // 验证 CRC 正确性：用 frame 去除最后2字节重新计算
        var dataWithoutCrc = frame[..^2];
        var expectedCrc = _checksumService.Calculate(ChecksumAlgorithmType.Crc16Modbus, dataWithoutCrc);
        frame[^2].Should().Be(expectedCrc[^1]); // CRC Lo
        frame[^1].Should().Be(expectedCrc[^2]); // CRC Hi
    }

    [Fact]
    public void BuildFrame_WithDefaultValues_ShouldUseConfigDefaults()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 5, startAddress: 100, quantity: 3);

        // 不传任何字段值，应使用配置默认值
        var frame = parser.BuildFrame([]);

        frame[0].Should().Be(0x05); // Config default SlaveId
        frame[1].Should().Be(0x03); // FC 03
        frame[2].Should().Be(0x00); // Start Addr Hi (100 = 0x0064)
        frame[3].Should().Be(0x64); // Start Addr Lo
        frame[4].Should().Be(0x00); // Quantity Hi (3 = 0x0003)
        frame[5].Should().Be(0x03); // Quantity Lo
    }

    #endregion

    #region 往返测试 (Round-trip)

    [Fact]
    public void RoundTrip_FC03_BuildThenParse_ShouldBeConsistent()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "温度",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 1, fields: fields);

        // 构建请求帧
        var requestFrame = parser.BuildFrame(new Dictionary<string, object>
        {
            ["从站地址"] = "1"
        });

        // 解析请求帧
        var result = parser.Parse(requestFrame);
        result.IsValid.Should().BeTrue();
        result.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_FC03Response_BuildResponseManuallyThenParse_ShouldExtractFields()
    {
        var fields = new List<FieldDefinition>
        {
            new()
            {
                Name = "温度",
                StartIndex = 0,
                Length = 2,
                DataType = DataType.Int16,
                Endianness = Endianness.BigEndian
            },
            new()
            {
                Name = "湿度",
                StartIndex = 2,
                Length = 2,
                DataType = DataType.UInt16,
                Endianness = Endianness.BigEndian
            }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 2, fields: fields);

        // 手工构造响应帧: 温度=-5 (0xFFFB), 湿度=65 (0x0041)
        var frameData = new byte[] { 0x01, 0x03, 0x04, 0xFF, 0xFB, 0x00, 0x41 };
        var frame = AppendCrc(frameData);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        result.GetValue<short>("温度").Should().Be(-5);
        result.GetValue<ushort>("湿度").Should().Be(65);
    }

    #endregion

    #region 功能码模型测试

    [Fact]
    public void ModbusFunctionCode_IsReadOperation_ShouldIdentifyCorrectly()
    {
        ModbusFunctionCode.ReadHoldingRegisters.IsReadOperation().Should().BeTrue();
        ModbusFunctionCode.ReadInputRegisters.IsReadOperation().Should().BeTrue();
        ModbusFunctionCode.WriteSingleRegister.IsReadOperation().Should().BeFalse();
        ModbusFunctionCode.WriteMultipleRegisters.IsReadOperation().Should().BeFalse();
    }

    [Fact]
    public void ModbusFunctionCode_IsWriteOperation_ShouldIdentifyCorrectly()
    {
        ModbusFunctionCode.WriteSingleRegister.IsWriteOperation().Should().BeTrue();
        ModbusFunctionCode.WriteMultipleRegisters.IsWriteOperation().Should().BeTrue();
        ModbusFunctionCode.ReadHoldingRegisters.IsWriteOperation().Should().BeFalse();
    }

    [Fact]
    public void ModbusFunctionCode_IsExceptionResponse_ShouldDetectHighBit()
    {
        ModbusFunctionCodeExtensions.IsExceptionResponse(0x83).Should().BeTrue();
        ModbusFunctionCodeExtensions.IsExceptionResponse(0x03).Should().BeFalse();
    }

    #endregion

    #region Parse Direction Detection

    [Fact]
    public void Parse_FC03RequestFrame_ShouldDetectAsRequest()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1, startAddress: 0, quantity: 10);
        // 请求帧: [01] [03] [00 00] [00 0A] [CRC x2]
        var frame = AppendCrc([0x01, 0x03, 0x00, 0x00, 0x00, 0x0A]);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result as ModbusRtuParsedFrame;
        modbusResult.Should().NotBeNull();
        modbusResult!.IsResponseFrame.Should().BeFalse();
        modbusResult.Fields.Should().Contain(f => f.Name == "帧方向" && f.Description == "请求");
    }

    [Fact]
    public void Parse_FC03ResponseFrame_ShouldDetectAsResponse()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1);
        // 响应帧: [01] [03] [04] [00 01 00 02] [CRC x2]
        var frame = AppendCrc([0x01, 0x03, 0x04, 0x00, 0x01, 0x00, 0x02]);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result as ModbusRtuParsedFrame;
        modbusResult.Should().NotBeNull();
        modbusResult!.IsResponseFrame.Should().BeTrue();
        modbusResult.Fields.Should().Contain(f => f.Name == "帧方向" && f.Description == "响应");
    }

    [Fact]
    public void Parse_FC10ResponseFrame_ShouldDetectAsResponse()
    {
        var parser = CreateParser(ModbusFunctionCode.WriteMultipleRegisters, slaveId: 2, startAddress: 10, quantity: 3);
        // FC10 响应帧: [02] [10] [00 0A] [00 03] [CRC x2] — 固定 8 字节
        var frame = AppendCrc([0x02, 0x10, 0x00, 0x0A, 0x00, 0x03]);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result as ModbusRtuParsedFrame;
        modbusResult.Should().NotBeNull();
        modbusResult!.IsResponseFrame.Should().BeTrue();
        modbusResult.Fields.Should().Contain(f => f.Name == "帧方向" && f.Description == "响应");
    }

    [Fact]
    public void Parse_FC10RequestFrame_ShouldDetectAsRequest()
    {
        var parser = CreateParser(ModbusFunctionCode.WriteMultipleRegisters, slaveId: 2, startAddress: 10, quantity: 2);
        // FC10 请求帧: [02] [10] [00 0A] [00 02] [04] [00 01 00 02] [CRC x2]
        var frame = AppendCrc([0x02, 0x10, 0x00, 0x0A, 0x00, 0x02, 0x04, 0x00, 0x01, 0x00, 0x02]);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result as ModbusRtuParsedFrame;
        modbusResult.Should().NotBeNull();
        modbusResult!.IsResponseFrame.Should().BeFalse();
        modbusResult.Fields.Should().Contain(f => f.Name == "帧方向" && f.Description == "请求");
    }

    [Fact]
    public void Parse_ExceptionResponse_ShouldDetectAsResponse()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1);
        // 异常响应: [01] [83] [02] [CRC x2]
        var frame = AppendCrc([0x01, 0x83, 0x02]);

        var result = parser.Parse(frame);

        result.IsValid.Should().BeTrue();
        var modbusResult = result as ModbusRtuParsedFrame;
        modbusResult.Should().NotBeNull();
        modbusResult!.IsResponseFrame.Should().BeTrue();
        modbusResult!.IsExceptionResponse.Should().BeTrue();
        modbusResult.Fields.Should().Contain(f => f.Name == "帧方向" && f.Description == "响应");
    }

    #endregion

    #region GetBuildFieldInputs

    [Fact]
    public void GetBuildFieldInputs_ShouldIncludeSlaveIdAndDirection()
    {
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, slaveId: 1);

        var inputs = parser.GetBuildFieldInputs();

        inputs.Should().HaveCountGreaterThanOrEqualTo(2);
        inputs[0].FieldName.Should().Be("从站地址");
        inputs[1].FieldName.Should().Be("帧方向");
        inputs[1].IsToggleMode.Should().BeTrue();
        inputs[1].ToggleFalseLabel.Should().Be("请求");
        inputs[1].ToggleTrueLabel.Should().Be("响应");
    }

    [Fact]
    public void GetBuildFieldInputs_WithCustomFields_ShouldIncludeAll()
    {
        var fields = new List<FieldDefinition>
        {
            new() { Name = "温度", DataType = DataType.UInt16, IsEnabled = true },
            new() { Name = "湿度", DataType = DataType.UInt16, IsEnabled = true }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, fields: fields);

        var inputs = parser.GetBuildFieldInputs();

        inputs.Should().HaveCount(4); // 从站地址 + 帧方向 + 温度 + 湿度
        inputs[2].FieldName.Should().Be("温度");
        inputs[3].FieldName.Should().Be("湿度");
    }

    [Fact]
    public void GetBuildFieldInputs_ShouldFilterDisabledFields()
    {
        var fields = new List<FieldDefinition>
        {
            new() { Name = "启用字段", DataType = DataType.UInt16, IsEnabled = true },
            new() { Name = "禁用字段", DataType = DataType.UInt16, IsEnabled = false }
        };
        var parser = CreateParser(ModbusFunctionCode.ReadHoldingRegisters, fields: fields);

        var inputs = parser.GetBuildFieldInputs();

        inputs.Should().HaveCount(3); // 从站地址 + 帧方向 + 启用字段
        inputs.Should().NotContain(i => i.FieldName == "禁用字段");
    }

    #endregion
}
