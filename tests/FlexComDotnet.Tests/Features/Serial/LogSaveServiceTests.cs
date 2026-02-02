using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// LogSaveService 测试
/// </summary>
public class LogSaveServiceTests : IDisposable
{
    private readonly LogSaveService _service;
    private readonly string _testDir;

    public LogSaveServiceTests()
    {
        _service = new LogSaveService();
        _testDir = Path.Combine(Path.GetTempPath(), $"log_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        // 清理测试目录
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
        GC.SuppressFinalize(this);
    }

    private string GetTestFilePath(string filename) => Path.Combine(_testDir, filename);

    #region GetRecommendedExtension Tests

    [Theory]
    [InlineData(LogSaveFormat.Text, ".txt")]
    [InlineData(LogSaveFormat.Binary, ".bin")]
    [InlineData(LogSaveFormat.BinaryWithTimestamp, ".bin")]
    public void GetRecommendedExtension_ShouldReturnCorrectExtension(LogSaveFormat format, string expected)
    {
        // Act
        var result = _service.GetRecommendedExtension(format);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Save Text Format Tests

    [Fact]
    public void Save_TextFormat_ShouldCreateFile()
    {
        // Arrange
        var filePath = GetTestFilePath("test.txt");
        var records = new[]
        {
            new LogRecord([0x01, 0x02, 0x03], false, DateTime.Now),
            new LogRecord([0x04, 0x05], true, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Text };

        // Act
        var result = _service.Save(filePath, records, options);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void Save_TextFormat_ShouldContainCorrectContent()
    {
        // Arrange
        var filePath = GetTestFilePath("test_content.txt");
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 45, 123);
        var records = new[]
        {
            new LogRecord([0x48, 0x65, 0x6C, 0x6C, 0x6F], false, timestamp) // "Hello"
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Text, UseHexFormat = false };

        // Act
        _service.Save(filePath, records, options);
        var content = File.ReadAllText(filePath);

        // Assert
        content.Should().Contain("[RX]");
        content.Should().Contain("Hello");
        content.Should().Contain("2024-01-15 10:30:45.123");
    }

    [Fact]
    public void Save_TextFormat_HexMode_ShouldContainHexData()
    {
        // Arrange
        var filePath = GetTestFilePath("test_hex.txt");
        var records = new[]
        {
            new LogRecord([0x01, 0x02, 0xFF], true, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Text, UseHexFormat = true };

        // Act
        _service.Save(filePath, records, options);
        var content = File.ReadAllText(filePath);

        // Assert
        content.Should().Contain("[TX]");
        content.Should().Contain("01 02 FF");
    }

    [Fact]
    public void Save_TextFormat_FilterRxOnly_ShouldExcludeTx()
    {
        // Arrange
        var filePath = GetTestFilePath("test_rx_only.txt");
        var records = new[]
        {
            new LogRecord([0x01], false, DateTime.Now),
            new LogRecord([0x02], true, DateTime.Now),
            new LogRecord([0x03], false, DateTime.Now)
        };
        var options = new LogSaveOptions
        {
            Format = LogSaveFormat.Text,
            IncludeTx = false,
            IncludeRx = true
        };

        // Act
        _service.Save(filePath, records, options);
        var lines = File.ReadAllLines(filePath);

        // Assert
        lines.Should().HaveCount(2);
        lines.All(l => l.Contains("[RX]")).Should().BeTrue();
    }

    [Fact]
    public void Save_TextFormat_FilterTxOnly_ShouldExcludeRx()
    {
        // Arrange
        var filePath = GetTestFilePath("test_tx_only.txt");
        var records = new[]
        {
            new LogRecord([0x01], false, DateTime.Now),
            new LogRecord([0x02], true, DateTime.Now),
            new LogRecord([0x03], true, DateTime.Now)
        };
        var options = new LogSaveOptions
        {
            Format = LogSaveFormat.Text,
            IncludeTx = true,
            IncludeRx = false
        };

        // Act
        _service.Save(filePath, records, options);
        var lines = File.ReadAllLines(filePath);

        // Assert
        lines.Should().HaveCount(2);
        lines.All(l => l.Contains("[TX]")).Should().BeTrue();
    }

    #endregion

    #region Save Binary Format Tests

    [Fact]
    public void Save_BinaryFormat_ShouldCreateFile()
    {
        // Arrange
        var filePath = GetTestFilePath("test.bin");
        var records = new[]
        {
            new LogRecord([0x01, 0x02, 0x03], false, DateTime.Now),
            new LogRecord([0x04, 0x05], true, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Binary };

        // Act
        var result = _service.Save(filePath, records, options);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void Save_BinaryFormat_ShouldContainRawData()
    {
        // Arrange
        var filePath = GetTestFilePath("test_raw.bin");
        var records = new[]
        {
            new LogRecord([0x01, 0x02, 0x03], false, DateTime.Now),
            new LogRecord([0x04, 0x05], true, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Binary };

        // Act
        _service.Save(filePath, records, options);
        var bytes = File.ReadAllBytes(filePath);

        // Assert
        bytes.Should().Equal([0x01, 0x02, 0x03, 0x04, 0x05]);
    }

    [Fact]
    public void Save_BinaryWithTimestamp_ShouldContainHeader()
    {
        // Arrange
        var filePath = GetTestFilePath("test_ts.bin");
        var records = new[]
        {
            new LogRecord([0x01, 0x02], false, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.BinaryWithTimestamp };

        // Act
        _service.Save(filePath, records, options);
        var bytes = File.ReadAllBytes(filePath);

        // Assert - 检查文件头 "FLEXCOM" + 版本号
        bytes.Take(7).Should().Equal("FLEXCOM"u8.ToArray());
        bytes[7].Should().Be(1); // 版本号
    }

    #endregion

    #region Async Save Tests

    [Fact]
    public async Task SaveAsync_TextFormat_ShouldCreateFile()
    {
        // Arrange
        var filePath = GetTestFilePath("test_async.txt");
        var records = new[]
        {
            new LogRecord([0x01, 0x02], false, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Text };

        // Act
        var result = await _service.SaveAsync(filePath, records, options);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_BinaryFormat_ShouldCreateFile()
    {
        // Arrange
        var filePath = GetTestFilePath("test_async.bin");
        var records = new[]
        {
            new LogRecord([0x01, 0x02], false, DateTime.Now)
        };
        var options = new LogSaveOptions { Format = LogSaveFormat.Binary };

        // Act
        var result = await _service.SaveAsync(filePath, records, options);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Save_InvalidPath_ShouldReturnFalse()
    {
        // Arrange
        var filePath = Path.Combine("Z:\\NonExistent\\Path", "test.txt");
        var records = new[] { new LogRecord([0x01], false, DateTime.Now) };
        var options = new LogSaveOptions();

        // Act
        var result = _service.Save(filePath, records, options);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Save_EmptyRecords_ShouldCreateEmptyFile()
    {
        // Arrange
        var filePath = GetTestFilePath("empty.txt");
        var records = Array.Empty<LogRecord>();
        var options = new LogSaveOptions { Format = LogSaveFormat.Text };

        // Act
        var result = _service.Save(filePath, records, options);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().BeEmpty();
    }

    #endregion
}
