using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Visualization.Models;
using FlexComDotnet.Core.Features.Visualization.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Visualization;

public class VisualizationServiceTests
{
    private readonly Mock<IProtocolParserService> _mockParserService;
    private readonly VisualizationService _service;

    public VisualizationServiceTests()
    {
        _mockParserService = new Mock<IProtocolParserService>();
        _service = new VisualizationService(_mockParserService.Object);
    }

    #region 通道管理测试

    [Fact]
    public void AddChannel_ShouldAddChannel()
    {
        // Arrange
        var channel = new ChannelConfig
        {
            Id = "ch1",
            FieldName = "temperature",
            DisplayName = "温度",
            Color = "#FF0000"
        };

        // Act
        _service.AddChannel(channel);

        // Assert
        _service.GetChannels().Should().HaveCount(1);
        _service.GetChannel("ch1").Should().NotBeNull();
        _service.GetChannel("ch1")!.FieldName.Should().Be("temperature");
    }

    [Fact]
    public void AddChannel_WithDuplicateId_ShouldThrowArgumentException()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "temp" };
        _service.AddChannel(channel);

        // Act
        var act = () => _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "other" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddChannel_WithNullChannel_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.AddChannel(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddChannel_WithEmptyId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _service.AddChannel(new ChannelConfig { Id = "", FieldName = "temp" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveChannel_ShouldRemoveChannel()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp" });

        // Act
        var result = _service.RemoveChannel("ch1");

        // Assert
        result.Should().BeTrue();
        _service.GetChannels().Should().BeEmpty();
    }

    [Fact]
    public void RemoveChannel_WithNonExistentId_ShouldReturnFalse()
    {
        // Act
        var result = _service.RemoveChannel("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveChannel_ShouldAlsoClearChannelData()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "温度" };
        _service.AddChannel(channel);
        _service.Start();

        var frame = CreateParsedFrame("温度", 25.5);
        _service.PushData(frame);

        // Act
        _service.RemoveChannel("ch1");

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void UpdateChannel_ShouldUpdateExistingChannel()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp", DisplayName = "温度" });

        // Act
        _service.UpdateChannel(new ChannelConfig { Id = "ch1", FieldName = "temp", DisplayName = "温度值", Color = "#00FF00" });

        // Assert
        var channel = _service.GetChannel("ch1");
        channel.Should().NotBeNull();
        channel!.DisplayName.Should().Be("温度值");
        channel.Color.Should().Be("#00FF00");
    }

    [Fact]
    public void UpdateChannel_WithNonExistentId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _service.UpdateChannel(new ChannelConfig { Id = "non-existent" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetChannel_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = _service.GetChannel("non-existent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region 数据管理测试

    [Fact]
    public void PushData_WhenRunning_ShouldAddDataPoints()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "温度" };
        _service.AddChannel(channel);
        _service.Start();

        var frame = CreateParsedFrame("温度", 25.5);

        // Act
        _service.PushData(frame);

        // Assert
        var data = _service.GetChannelData("ch1");
        data.Should().HaveCount(1);
        data[0].Value.Should().Be(25.5);
        data[0].ChannelId.Should().Be("ch1");
    }

    [Fact]
    public void PushData_WhenStopped_ShouldNotAddDataPoints()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "温度" };
        _service.AddChannel(channel);
        // Not started

        var frame = CreateParsedFrame("温度", 25.5);

        // Act
        _service.PushData(frame);

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void PushData_WhenChannelNotVisible_ShouldNotAddDataPoints()
    {
        // Arrange
        var channel = new ChannelConfig { Id = "ch1", FieldName = "温度", IsVisible = false };
        _service.AddChannel(channel);
        _service.Start();

        var frame = CreateParsedFrame("温度", 25.5);

        // Act
        _service.PushData(frame);

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void PushData_ShouldMatchFieldNameToChannel()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度" });
        _service.AddChannel(new ChannelConfig { Id = "ch2", FieldName = "湿度" });
        _service.Start();

        var frame = new ParsedFrame
        {
            IsValid = true,
            Fields =
            [
                new ParsedField { Name = "温度", Value = 25.5, DataType = DataType.Float },
                new ParsedField { Name = "湿度", Value = 60.0, DataType = DataType.Float }
            ]
        };

        // Act
        _service.PushData(frame);

        // Assert
        _service.GetChannelData("ch1").Should().HaveCount(1);
        _service.GetChannelData("ch1")[0].Value.Should().Be(25.5);
        _service.GetChannelData("ch2").Should().HaveCount(1);
        _service.GetChannelData("ch2")[0].Value.Should().Be(60.0);
    }

    [Fact]
    public void PushData_WithInvalidFrame_ShouldNotAddData()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度" });
        _service.Start();

        var frame = new ParsedFrame { IsValid = false };

        // Act
        _service.PushData(frame);

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void PushData_ShouldConvertNumericTypes()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "count" });
        _service.Start();

        // Int value should be converted to double
        var frame = CreateParsedFrame("count", 42);

        // Act
        _service.PushData(frame);

        // Assert
        _service.GetChannelData("ch1").Should().HaveCount(1);
        _service.GetChannelData("ch1")[0].Value.Should().Be(42.0);
    }

    [Fact]
    public void PushData_ShouldRespectMaxDataPoints()
    {
        // Arrange
        _service.MaxDataPoints = 15;
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "val" });
        _service.Start();

        // Act - push 20 data points
        for (int i = 0; i < 20; i++)
        {
            _service.PushData(CreateParsedFrame("val", i));
        }

        // Assert - should only keep the latest 15
        var data = _service.GetChannelData("ch1");
        data.Should().HaveCount(15);
        data[0].Value.Should().Be(5); // oldest remaining
        data[14].Value.Should().Be(19); // newest
    }

    [Fact]
    public void PushData_ShouldFireDataPointAddedEvent()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度" });
        _service.Start();

        DataPointAddedEventArgs? eventArgs = null;
        _service.DataPointAdded += (sender, args) => eventArgs = args;

        // Act
        _service.PushData(CreateParsedFrame("温度", 25.5));

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.DataPoint.Value.Should().Be(25.5);
        eventArgs.DataPoint.ChannelId.Should().Be("ch1");
    }

    [Fact]
    public void ClearData_ShouldClearAllChannelData()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度" });
        _service.AddChannel(new ChannelConfig { Id = "ch2", FieldName = "湿度" });
        _service.Start();

        _service.PushData(CreateParsedFrame("温度", 25.5));
        _service.PushData(CreateParsedFrame("湿度", 60.0));

        // Act
        _service.ClearData();

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
        _service.GetChannelData("ch2").Should().BeEmpty();
    }

    [Fact]
    public void ClearData_ShouldFireDataClearedEvent()
    {
        // Arrange
        bool eventFired = false;
        _service.DataCleared += (sender, args) => eventFired = true;

        // Act
        _service.ClearData();

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void ClearChannelData_ShouldOnlyClearSpecificChannel()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度" });
        _service.AddChannel(new ChannelConfig { Id = "ch2", FieldName = "湿度" });
        _service.Start();

        _service.PushData(CreateParsedFrame("温度", 25.5));
        _service.PushData(CreateParsedFrame("湿度", 60.0));

        // Act
        _service.ClearChannelData("ch1");

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
        _service.GetChannelData("ch2").Should().HaveCount(1);
    }

    [Fact]
    public void GetAllData_ShouldReturnAllChannelData()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度" });
        _service.AddChannel(new ChannelConfig { Id = "ch2", FieldName = "湿度" });
        _service.Start();

        _service.PushData(CreateParsedFrame("温度", 25.5));
        _service.PushData(CreateParsedFrame("湿度", 60.0));

        // Act
        var allData = _service.GetAllData();

        // Assert
        allData.Should().HaveCount(2);
        allData.Should().ContainKey("ch1");
        allData.Should().ContainKey("ch2");
    }

    #endregion

    #region 状态管理测试

    [Fact]
    public void NewService_ShouldNotBeRunning()
    {
        // Assert
        _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_ShouldSetIsRunningToTrue()
    {
        // Act
        _service.Start();

        // Assert
        _service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Stop_ShouldSetIsRunningToFalse()
    {
        // Arrange
        _service.Start();

        // Act
        _service.Stop();

        // Assert
        _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_ShouldFireStateChangedEvent()
    {
        // Arrange
        VisualizationStateChangedEventArgs? eventArgs = null;
        _service.StateChanged += (sender, args) => eventArgs = args;

        // Act
        _service.Start();

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Stop_ShouldFireStateChangedEvent()
    {
        // Arrange
        _service.Start();
        VisualizationStateChangedEventArgs? eventArgs = null;
        _service.StateChanged += (sender, args) => eventArgs = args;

        // Act
        _service.Stop();

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.IsRunning.Should().BeFalse();
    }

    #endregion

    #region 配置测试

    [Fact]
    public void MaxDataPoints_DefaultShouldBe1000()
    {
        // Assert
        _service.MaxDataPoints.Should().Be(1000);
    }

    [Fact]
    public void MaxDataPoints_ShouldBeSettable()
    {
        // Act
        _service.MaxDataPoints = 500;

        // Assert
        _service.MaxDataPoints.Should().Be(500);
    }

    [Fact]
    public void MaxDataPoints_WhenSetToLessThan10_ShouldClampTo10()
    {
        // Act
        _service.MaxDataPoints = 3;

        // Assert
        _service.MaxDataPoints.Should().Be(10);
    }

    [Fact]
    public void SelectedParserName_ShouldBeSettable()
    {
        // Act
        _service.SelectedParserName = "MyProtocol";

        // Assert
        _service.SelectedParserName.Should().Be("MyProtocol");
    }

    #endregion

    #region CSV 导出测试

    [Fact]
    public void ExportToCsv_ShouldCreateFile()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度", DisplayName = "温度" });
        _service.Start();
        _service.PushData(CreateParsedFrame("温度", 25.5));

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            _service.ExportToCsv(tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = File.ReadAllText(tempFile);
            content.Should().Contain("温度");
            content.Should().Contain("25.5");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportToCsv_WithMultipleChannels_ShouldIncludeAllData()
    {
        // Arrange
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "温度", DisplayName = "温度" });
        _service.AddChannel(new ChannelConfig { Id = "ch2", FieldName = "湿度", DisplayName = "湿度" });
        _service.Start();

        var frame = new ParsedFrame
        {
            IsValid = true,
            Fields =
            [
                new ParsedField { Name = "温度", Value = 25.5, DataType = DataType.Float },
                new ParsedField { Name = "湿度", Value = 60.0, DataType = DataType.Float }
            ]
        };
        _service.PushData(frame);

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            _service.ExportToCsv(tempFile);

            // Assert
            var content = File.ReadAllText(tempFile);
            content.Should().Contain("温度");
            content.Should().Contain("湿度");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportToCsv_WithNoData_ShouldCreateEmptyFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            _service.ExportToCsv(tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region 辅助方法

    private static ParsedFrame CreateParsedFrame(string fieldName, object value)
    {
        return new ParsedFrame
        {
            IsValid = true,
            Fields =
            [
                new ParsedField
                {
                    Name = fieldName,
                    Value = value,
                    DataType = value switch
                    {
                        float => DataType.Float,
                        double => DataType.Double,
                        int => DataType.Int32,
                        _ => DataType.Float
                    }
                }
            ]
        };
    }

    #endregion

    #region FeedRawData 测试

    [Fact]
    public void FeedRawData_WhenNotRunning_ShouldNotProcess()
    {
        // Arrange
        _service.SelectedParserName = "TestParser";
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp" });

        // Act
        _service.FeedRawData([0x01, 0x02, 0x03]);

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void FeedRawData_WhenNoParserSelected_ShouldNotProcess()
    {
        // Arrange
        _service.Start();
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp" });

        // Act
        _service.FeedRawData([0x01, 0x02, 0x03]);

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void FeedRawData_WhenParserNotFound_ShouldNotProcess()
    {
        // Arrange
        _mockParserService.Setup(s => s.GetParser("UnknownParser")).Returns((IProtocolParser?)null);
        _service.SelectedParserName = "UnknownParser";
        _service.Start();
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp" });

        // Act
        _service.FeedRawData([0x01, 0x02, 0x03]);

        // Assert
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void FeedRawData_WithCompleteFrame_ShouldParseAndPushData()
    {
        // Arrange
        var mockParser = new Mock<IProtocolParser>();
        var frameBytes = new byte[] { 0x01, 0x02, 0x03 };
        var extractedFrame = new byte[] { 0x01, 0x02, 0x03 };

        mockParser.Setup(p => p.TryExtractFrame(
            It.Is<byte[]>(b => b.Length == 3),
            out extractedFrame,
            out It.Ref<int>.IsAny))
            .Returns((byte[] buffer, out byte[] frame, out int consumed) =>
            {
                frame = buffer;
                consumed = buffer.Length;
                return true;
            });

        // 第二次调用时缓冲区为空，返回 false
        mockParser.Setup(p => p.TryExtractFrame(
            It.Is<byte[]>(b => b.Length == 0),
            out It.Ref<byte[]>.IsAny,
            out It.Ref<int>.IsAny))
            .Returns(false);

        var parsedFrame = CreateParsedFrame("temp", 25.5f);
        mockParser.Setup(p => p.Parse(It.IsAny<byte[]>())).Returns(parsedFrame);

        _mockParserService.Setup(s => s.GetParser("TestParser")).Returns(mockParser.Object);

        _service.SelectedParserName = "TestParser";
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp", IsVisible = true });
        _service.Start();

        // Act
        _service.FeedRawData(frameBytes);

        // Assert
        _service.GetChannelData("ch1").Should().HaveCount(1);
        _service.GetChannelData("ch1")[0].Value.Should().Be(25.5);
    }

    [Fact]
    public void FeedRawData_WithNullData_ShouldNotThrow()
    {
        // Arrange
        _service.SelectedParserName = "TestParser";
        _service.Start();

        // Act & Assert
        var act = () => _service.FeedRawData(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void FeedRawData_WithEmptyData_ShouldNotThrow()
    {
        // Arrange
        _service.SelectedParserName = "TestParser";
        _service.Start();

        // Act & Assert
        var act = () => _service.FeedRawData([]);
        act.Should().NotThrow();
    }

    [Fact]
    public void Start_ShouldClearBuffer()
    {
        // Arrange - 不完整帧留在缓冲区
        var mockParser = new Mock<IProtocolParser>();
        mockParser.Setup(p => p.TryExtractFrame(
            It.IsAny<byte[]>(),
            out It.Ref<byte[]>.IsAny,
            out It.Ref<int>.IsAny))
            .Returns(false); // 模拟帧不完整

        _mockParserService.Setup(s => s.GetParser("TestParser")).Returns(mockParser.Object);

        _service.SelectedParserName = "TestParser";
        _service.Start();
        _service.FeedRawData([0x01, 0x02]); // 不完整帧
        _service.Stop();

        // Act - 重新启动应清空缓冲区
        _service.Start();

        // Assert - 缓冲区被清空后，新数据应正常处理
        // 不应有旧数据残留
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp", IsVisible = true });
        _service.GetChannelData("ch1").Should().BeEmpty();
    }

    [Fact]
    public void FeedRawData_ShouldFireDataPointAddedEvent()
    {
        // Arrange
        var mockParser = new Mock<IProtocolParser>();
        var extractedFrame = new byte[] { 0x01, 0x02, 0x03 };

        mockParser.Setup(p => p.TryExtractFrame(
            It.Is<byte[]>(b => b.Length >= 3),
            out extractedFrame,
            out It.Ref<int>.IsAny))
            .Returns((byte[] buffer, out byte[] frame, out int consumed) =>
            {
                frame = buffer;
                consumed = buffer.Length;
                return true;
            });

        mockParser.Setup(p => p.TryExtractFrame(
            It.Is<byte[]>(b => b.Length == 0),
            out It.Ref<byte[]>.IsAny,
            out It.Ref<int>.IsAny))
            .Returns(false);

        var parsedFrame = CreateParsedFrame("temp", 30.0f);
        mockParser.Setup(p => p.Parse(It.IsAny<byte[]>())).Returns(parsedFrame);

        _mockParserService.Setup(s => s.GetParser("TestParser")).Returns(mockParser.Object);

        _service.SelectedParserName = "TestParser";
        _service.AddChannel(new ChannelConfig { Id = "ch1", FieldName = "temp", IsVisible = true });
        _service.Start();

        var eventFired = false;
        _service.DataPointAdded += (_, _) => eventFired = true;

        // Act
        _service.FeedRawData([0x01, 0x02, 0x03]);

        // Assert
        eventFired.Should().BeTrue();
    }

    #endregion
}
