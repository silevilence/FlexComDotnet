using System.Text;
using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Logging;

public class LogPersistenceServiceTests : IDisposable
{
    private readonly string _testDir;

    public LogPersistenceServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FlexComDotnet_LogTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private string ReadLogFile()
    {
        var filePath = Path.Combine(_testDir, $"{DateTime.Now:yyyy-MM-dd}.log");
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private string[] ReadLogFileLines()
    {
        return ReadLogFile().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void WriteSessionStart_ShouldCreateDateBasedFile()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);

        // Act
        service.WriteSessionStart();
        service.Flush();

        // Assert
        var expectedFile = Path.Combine(_testDir, $"{DateTime.Now:yyyy-MM-dd}.log");
        File.Exists(expectedFile).Should().BeTrue();
    }

    [Fact]
    public void WriteSessionStart_ShouldWriteSessionMarker()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);

        // Act
        service.WriteSessionStart();
        service.Flush();

        // Assert
        var content = ReadLogFile();
        content.Should().Contain("=== FlexComDotnet Session Start");
    }

    [Fact]
    public void Write_ShouldPersistLogEntry()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);
        service.WriteSessionStart();

        var entry = new LogEntry
        {
            Timestamp = new DateTime(2026, 3, 7, 10, 30, 0, 123),
            Level = LogLevel.Info,
            Source = LogSource.Serial,
            Message = "测试持久化消息"
        };

        // Act
        service.Write(entry);
        service.Flush();

        // Assert
        var content = ReadLogFile();
        content.Should().Contain("[INFO]");
        content.Should().Contain("[串口]");
        content.Should().Contain("测试持久化消息");
    }

    [Fact]
    public void Write_MultipleEntries_ShouldPersistAll()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);
        service.WriteSessionStart();

        // Act
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.System, Message = "消息1" });
        service.Write(new LogEntry { Level = LogLevel.Warning, Source = LogSource.Script, Message = "消息2" });
        service.Write(new LogEntry { Level = LogLevel.Error, Source = LogSource.Network, Message = "消息3" });
        service.Flush();

        // Assert
        var lines = ReadLogFileLines();
        // Session start marker + 3 log entries
        lines.Count(l => l.Contains("消息")).Should().Be(3);
    }

    [Fact]
    public void WriteSessionEnd_ShouldWriteEndMarker()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);
        service.WriteSessionStart();
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.System, Message = "test" });

        // Act
        service.WriteSessionEnd();
        service.Flush();

        // Assert
        var content = ReadLogFile();
        content.Should().Contain("=== FlexComDotnet Session End");
    }

    [Fact]
    public void Write_ShouldFormatLogSourceInChinese()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);
        service.WriteSessionStart();

        // Act
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.Serial, Message = "test" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.Network, Message = "test" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.Script, Message = "test" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.AutoReply, Message = "test" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.Protocol, Message = "test" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.Visualization, Message = "test" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.System, Message = "test" });
        service.Flush();

        // Assert
        var content = ReadLogFile();
        content.Should().Contain("[串口]");
        content.Should().Contain("[网络]");
        content.Should().Contain("[脚本]");
        content.Should().Contain("[自动回复]");
        content.Should().Contain("[协议]");
        content.Should().Contain("[可视化]");
        content.Should().Contain("[系统]");
    }

    [Fact]
    public void Write_ShouldFormatLogLevelCorrectly()
    {
        // Arrange
        using var service = new LogPersistenceService(_testDir);
        service.WriteSessionStart();

        // Act
        service.Write(new LogEntry { Level = LogLevel.Debug, Source = LogSource.System, Message = "d" });
        service.Write(new LogEntry { Level = LogLevel.Info, Source = LogSource.System, Message = "i" });
        service.Write(new LogEntry { Level = LogLevel.Warning, Source = LogSource.System, Message = "w" });
        service.Write(new LogEntry { Level = LogLevel.Error, Source = LogSource.System, Message = "e" });
        service.Flush();

        // Assert
        var content = ReadLogFile();
        content.Should().Contain("[DEBUG]");
        content.Should().Contain("[INFO]");
        content.Should().Contain("[WARNING]");
        content.Should().Contain("[ERROR]");
    }
}
