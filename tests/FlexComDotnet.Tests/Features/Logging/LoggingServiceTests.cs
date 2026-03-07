using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Logging;

public class LoggingServiceTests
{
    #region 基本日志记录

    [Fact]
    public void Log_ShouldAddEntryToCollection()
    {
        // Arrange
        var service = new LoggingService();

        // Act
        service.Log(LogLevel.Info, LogSource.System, "测试消息");

        // Assert
        service.Entries.Should().HaveCount(1);
        service.Entries[0].Level.Should().Be(LogLevel.Info);
        service.Entries[0].Source.Should().Be(LogSource.System);
        service.Entries[0].Message.Should().Be("测试消息");
    }

    [Fact]
    public void Log_ShouldSetTimestamp()
    {
        // Arrange
        var service = new LoggingService();
        var before = DateTime.Now;

        // Act
        service.Log(LogLevel.Info, LogSource.Serial, "test");

        // Assert
        var after = DateTime.Now;
        service.Entries[0].Timestamp.Should().BeOnOrAfter(before);
        service.Entries[0].Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Log_ShouldFireLogAddedEvent()
    {
        // Arrange
        var service = new LoggingService();
        LogEntry? receivedEntry = null;
        service.LogAdded += (_, entry) => receivedEntry = entry;

        // Act
        service.Log(LogLevel.Warning, LogSource.Script, "警告消息");

        // Assert
        receivedEntry.Should().NotBeNull();
        receivedEntry!.Level.Should().Be(LogLevel.Warning);
        receivedEntry.Source.Should().Be(LogSource.Script);
        receivedEntry.Message.Should().Be("警告消息");
    }

    #endregion

    #region 快捷方法

    [Fact]
    public void Info_ShouldLogWithInfoLevel()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "信息");
        service.Entries[0].Level.Should().Be(LogLevel.Info);
    }

    [Fact]
    public void Warning_ShouldLogWithWarningLevel()
    {
        var service = new LoggingService();
        service.Warning(LogSource.Network, "警告");
        service.Entries[0].Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Error_ShouldLogWithErrorLevel()
    {
        var service = new LoggingService();
        service.Error(LogSource.AutoReply, "错误");
        service.Entries[0].Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void Debug_ShouldLogWithDebugLevel()
    {
        var service = new LoggingService();
        service.Debug(LogSource.Protocol, "调试");
        service.Entries[0].Level.Should().Be(LogLevel.Debug);
    }

    #endregion

    #region 多条日志

    [Fact]
    public void Log_MultipleEntries_ShouldPreserveOrder()
    {
        // Arrange
        var service = new LoggingService();

        // Act
        service.Info(LogSource.Serial, "第一条");
        service.Warning(LogSource.Script, "第二条");
        service.Error(LogSource.Network, "第三条");

        // Assert
        service.Entries.Should().HaveCount(3);
        service.Entries[0].Message.Should().Be("第一条");
        service.Entries[1].Message.Should().Be("第二条");
        service.Entries[2].Message.Should().Be("第三条");
    }

    #endregion

    #region 持久化集成

    [Fact]
    public void Log_WithPersistence_ShouldWriteToFile()
    {
        // Arrange
        var persistence = new MockLogPersistenceService();
        var service = new LoggingService(persistence);

        // Act
        service.Info(LogSource.System, "持久化测试");

        // Assert
        persistence.WrittenEntries.Should().HaveCount(1);
        persistence.WrittenEntries[0].Message.Should().Be("持久化测试");
    }

    [Fact]
    public void Log_WithoutPersistence_ShouldNotThrow()
    {
        // Arrange
        var service = new LoggingService();

        // Act & Assert
        var action = () => service.Info(LogSource.System, "无持久化");
        action.Should().NotThrow();
    }

    #endregion

    /// <summary>
    /// 模拟持久化服务
    /// </summary>
    private class MockLogPersistenceService : ILogPersistenceService
    {
        public List<LogEntry> WrittenEntries { get; } = [];
        public bool SessionStartWritten { get; private set; }
        public bool SessionEndWritten { get; private set; }

        public void Write(LogEntry entry) => WrittenEntries.Add(entry);
        public void WriteSessionStart() => SessionStartWritten = true;
        public void WriteSessionEnd() => SessionEndWritten = true;
        public void Flush() { }
        public void Dispose() { }
    }
}
