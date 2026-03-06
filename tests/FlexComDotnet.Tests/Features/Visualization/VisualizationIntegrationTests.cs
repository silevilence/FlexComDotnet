using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Visualization.Models;
using FlexComDotnet.Core.Features.Visualization.Services;
using FlexComDotnet.Core.Features.Visualization.ViewModels;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Visualization;

/// <summary>
/// 集成测试：模拟用户真实使用场景，验证从原始字节到数据点的完整管道
/// </summary>
public class VisualizationIntegrationTests
{
    /// <summary>
    /// 模拟用户操作：创建"新协议" → 添加 Field1 通道 → 开始 → 接收数据
    /// </summary>
    [Fact]
    public void EndToEnd_NewProtocolWithField1_RawAsciiData_ShouldProduceDataPoints()
    {
        // Arrange: 创建真实的 ProtocolParserService
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);

        // 模拟用户在协议解析器中创建"新协议"
        var definition = new FrameDefinition
        {
            Name = "新协议",
            Description = "协议描述",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };

        // 添加 Field1 (模拟用户点击"添加字段")
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 0,
            IsEnabled = true
        });

        parserService.RegisterDefinition(definition);

        // 创建 VisualizationService
        var vizService = new VisualizationService(parserService);

        // 模拟用户添加 Field1 通道
        vizService.AddChannel(new ChannelConfig
        {
            Id = "ch1",
            FieldName = "Field1",
            DisplayName = "Field1",
            Color = "#2196F3",
            IsVisible = true,
            Order = 0
        });

        // 模拟用户点击"开始"
        vizService.SelectedParserName = "新协议";
        vizService.Start();

        // 记录数据点事件
        var dataPointCount = 0;
        vizService.DataPointAdded += (_, _) => dataPointCount++;

        // Act: 模拟串口接收 "Welcome to UartAssist" (21 字节)
        var rawData = "Welcome to UartAssist"u8.ToArray();
        vizService.FeedRawData(rawData);

        // Assert
        dataPointCount.Should().Be(21, "每个字节应产生一个 Field1 数据点");
        vizService.GetChannelData("ch1").Should().HaveCount(21);

        // 验证第一个数据点的值 (W = 0x57 = 87)
        vizService.GetChannelData("ch1")[0].Value.Should().Be(87);
    }

    /// <summary>
    /// 验证 TryExtractFrame 对无帧头协议的行为
    /// </summary>
    [Fact]
    public void ConfigurableParser_NoHeader_MinFrameLength1_ShouldExtractSingleBytes()
    {
        // Arrange
        var checksumService = new ChecksumService();
        var definition = new FrameDefinition
        {
            Name = "测试",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 0,
            IsEnabled = true
        });

        var parserService = new ProtocolParserService(checksumService);
        parserService.RegisterDefinition(definition);
        var parser = parserService.GetParser("测试");
        parser.Should().NotBeNull();

        // Act: TryExtractFrame 应该从 3 字节缓冲区提取 1 字节帧
        var buffer = new byte[] { 0x41, 0x42, 0x43 };
        var result = parser!.TryExtractFrame(buffer, out var frame, out var consumed);

        // Assert
        result.Should().BeTrue("无帧头时应能提取 MinFrameLength 字节");
        frame.Should().HaveCount(1);
        frame[0].Should().Be(0x41);
        consumed.Should().Be(1);
    }

    /// <summary>
    /// 验证 Parse 对单字节帧的解析
    /// </summary>
    [Fact]
    public void ConfigurableParser_Parse_SingleByteFrame_ShouldReturnField1Value()
    {
        // Arrange
        var checksumService = new ChecksumService();
        var definition = new FrameDefinition
        {
            Name = "测试",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 0,
            IsEnabled = true
        });

        var parserService = new ProtocolParserService(checksumService);
        parserService.RegisterDefinition(definition);
        var parser = parserService.GetParser("测试")!;

        // Act
        var result = parser.Parse([0x57]);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Fields.Should().HaveCount(1);
        result.Fields[0].Name.Should().Be("Field1");
        result.Fields[0].Value.Should().Be((byte)0x57);
    }

    /// <summary>
    /// 验证 PushData 能否处理 UInt8 值
    /// </summary>
    [Fact]
    public void PushData_UInt8Value_ShouldConvertAndAddDataPoint()
    {
        // Arrange
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);
        var vizService = new VisualizationService(parserService);

        vizService.AddChannel(new ChannelConfig
        {
            Id = "ch1",
            FieldName = "Field1",
            IsVisible = true
        });
        vizService.Start();

        var frame = new ParsedFrame
        {
            IsValid = true,
            Fields =
            [
                new ParsedField
                {
                    Name = "Field1",
                    Value = (byte)87,  // UInt8 值
                    DataType = DataType.UInt8
                }
            ]
        };

        // Act
        vizService.PushData(frame);

        // Assert
        vizService.GetChannelData("ch1").Should().HaveCount(1);
        vizService.GetChannelData("ch1")[0].Value.Should().Be(87);
    }

    /// <summary>
    /// 验证无通道时 PushData 不会产生数据点（这是根本原因）
    /// </summary>
    [Fact]
    public void PushData_WithNoChannels_ShouldNotProduceDataPoints()
    {
        // Arrange
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);
        var vizService = new VisualizationService(parserService);

        // 不添加任何通道！
        vizService.Start();

        var eventFired = false;
        vizService.DataPointAdded += (_, _) => eventFired = true;

        var frame = new ParsedFrame
        {
            IsValid = true,
            Fields =
            [
                new ParsedField
                {
                    Name = "Field1",
                    Value = (byte)87,
                    DataType = DataType.UInt8
                }
            ]
        };

        // Act
        vizService.PushData(frame);

        // Assert: 没有通道 → 没有数据点 → 事件不触发
        eventFired.Should().BeFalse("无通道时不应产生任何数据点");
    }

    /// <summary>
    /// 完整端到端测试：无通道时 FeedRawData 也不会产生数据点
    /// </summary>
    [Fact]
    public void FeedRawData_WithNoChannels_ShouldNotProduceDataPoints()
    {
        // Arrange
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);

        var definition = new FrameDefinition
        {
            Name = "TestProto",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 0,
            IsEnabled = true
        });
        parserService.RegisterDefinition(definition);

        var vizService = new VisualizationService(parserService);
        // 不添加通道！
        vizService.SelectedParserName = "TestProto";
        vizService.Start();

        var eventCount = 0;
        vizService.DataPointAdded += (_, _) => eventCount++;

        // Act
        vizService.FeedRawData(new byte[] { 0x41, 0x42, 0x43 });

        // Assert: 没有通道 → 帧提取和解析正常，但不产生数据点
        eventCount.Should().Be(0, "无通道时不应产生数据点");
    }

    /// <summary>
    /// 完整端到端测试：模拟真实使用场景（ViewModel + Service），点击开始后接收数据
    /// 这是复现用户报告 "数据点: 0" 的最接近真实场景的测试
    /// </summary>
    [Fact]
    public void FullScenario_ViewModelStartThenFeedData_ShouldProduceDataPoints()
    {
        // Arrange: 创建真实的服务实例（不用 Mock）
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);

        // 注册 "新协议" - 和用户在 UI 中创建的一样
        var definition = new FrameDefinition
        {
            Name = "新协议",
            Description = "",
            ProtocolType = ProtocolType.Generic,
            MinFrameLength = 1
        };
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 0,
            IsEnabled = true
        });
        parserService.RegisterDefinition(definition);

        // 创建服务和 ViewModel（和 DI 容器中一样）
        var vizService = new VisualizationService(parserService);

        // 确保 ViewModel 在没有 SynchronizationContext 的情况下创建
        var originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        DataVisualizationViewModel viewModel;
        try
        {
            viewModel = new DataVisualizationViewModel(vizService, parserService);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        // 模拟用户选择 "新协议"
        viewModel.SelectedParserName = "新协议";

        // 模拟用户点击 "开始"
        viewModel.StartCommand.Execute(null);

        // 验证：通道应该被自动添加
        viewModel.Channels.Should().HaveCount(1, "Field1 应被自动添加为通道");
        viewModel.Channels[0].FieldName.Should().Be("Field1");
        viewModel.StatusMessage.Should().Be("采集中...");

        // 模拟串口收到 "Welcome to UartAssist" (和截图中的 RX 数据一致)
        var rawData = "Welcome to UartAssist"u8.ToArray();
        vizService.FeedRawData(rawData);

        // 验证数据点
        var channelId = viewModel.Channels[0].Id;
        var dataPoints = vizService.GetChannelData(channelId);
        dataPoints.Should().HaveCount(21, "21 字节应产生 21 个数据点");
        dataPoints[0].Value.Should().Be(87, "'W' = 0x57 = 87");

        // 验证 ViewModel 接收到了数据点事件（服务层产生了21个数据点）
        viewModel.TotalDataPoints.Should().Be(21);
    }

    /// <summary>
    /// 验证协议帧头不匹配时触发 ExtractionFailed 事件并在 ViewModel 显示警告
    /// 这正是用户遇到的问题场景：协议配置了 Header='12' 但接收的是 ASCII 数据
    /// </summary>
    [Fact]
    public void FeedRawData_WhenProtocolHeaderDoesNotMatchData_ShouldFireExtractionFailedEvent()
    {
        // Arrange: 创建有帧头的协议（模拟用户的 "新协议" 配置了 Header='12'）
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);

        var definition = new FrameDefinition
        {
            Name = "带帧头的协议",
            ProtocolType = ProtocolType.Generic,
            Header = "12",  // 帧头 0x12
            MinFrameLength = 3
        };
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 1,
            IsEnabled = true
        });
        parserService.RegisterDefinition(definition);

        var vizService = new VisualizationService(parserService);
        vizService.AddChannel(new ChannelConfig
        {
            Id = "ch1",
            FieldName = "Field1",
            IsVisible = true
        });

        vizService.SelectedParserName = "带帧头的协议";
        vizService.Start();

        ExtractionFailedEventArgs? failedArgs = null;
        vizService.ExtractionFailed += (_, args) => failedArgs = args;

        // Act: 发送不包含 0x12 的 ASCII 数据（和用户的场景一样）
        var data = "Welcome to UartAssist - this is test data with more than fifty bytes padding"u8.ToArray();
        vizService.FeedRawData(data);

        // Assert: 应该触发 ExtractionFailed 事件
        failedArgs.Should().NotBeNull("协议帧头不匹配时应通知用户");
        failedArgs!.BytesReceived.Should().BeGreaterThanOrEqualTo(50);
        failedArgs.FramesExtracted.Should().Be(0);
        vizService.GetChannelData("ch1").Should().HaveCount(0, "帧头不匹配不应产生数据点");
    }

    /// <summary>
    /// 验证 ViewModel 收到 ExtractionFailed 后更新 StatusMessage
    /// </summary>
    [Fact]
    public void ViewModel_WhenExtractionFails_ShouldUpdateStatusMessage()
    {
        // Arrange
        var checksumService = new ChecksumService();
        var parserService = new ProtocolParserService(checksumService);

        var definition = new FrameDefinition
        {
            Name = "测试协议",
            ProtocolType = ProtocolType.Generic,
            Header = "AA BB",  // 帧头 0xAA 0xBB
            MinFrameLength = 4
        };
        definition.Fields.Add(new FieldDefinition
        {
            Name = "Field1",
            DataType = DataType.UInt8,
            Length = 1,
            StartIndex = 2,
            IsEnabled = true
        });
        parserService.RegisterDefinition(definition);

        var vizService = new VisualizationService(parserService);

        // 确保 ViewModel 在没有 SynchronizationContext 的情况下创建（避免异步分发）
        var originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        DataVisualizationViewModel viewModel;
        try
        {
            viewModel = new DataVisualizationViewModel(vizService, parserService);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        viewModel.SelectedParserName = "测试协议";
        viewModel.StartCommand.Execute(null);
        viewModel.StatusMessage.Should().Be("采集中...");

        // Act: 发送不匹配协议的数据
        var data = "This data does not match the protocol header AABB at all, need enough bytes"u8.ToArray();
        vizService.FeedRawData(data);

        // Assert: ViewModel 应更新状态消息为警告
        viewModel.StatusMessage.Should().Contain("帧提取失败");
        viewModel.TotalDataPoints.Should().Be(0);
    }
}
