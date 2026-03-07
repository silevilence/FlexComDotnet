using FlexComDotnet.Core.Features.Logging.Models;

namespace FlexComDotnet.Core.Features.Logging.Services;

/// <summary>
/// 统一日志服务实现
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly List<LogEntry> _entries = [];
    private readonly ILogPersistenceService? _persistenceService;
    private readonly object _lock = new();

    public LoggingService(ILogPersistenceService? persistenceService = null)
    {
        _persistenceService = persistenceService;
    }

    /// <inheritdoc/>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<LogEntry>? LogAdded;

    /// <inheritdoc/>
    public void Log(LogLevel level, LogSource source, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Source = source,
            Message = message
        };

        lock (_lock)
        {
            _entries.Add(entry);
        }

        _persistenceService?.Write(entry);
        LogAdded?.Invoke(this, entry);
    }

    /// <inheritdoc/>
    public void Info(LogSource source, string message) =>
        Log(LogLevel.Info, source, message);

    /// <inheritdoc/>
    public void Warning(LogSource source, string message) =>
        Log(LogLevel.Warning, source, message);

    /// <inheritdoc/>
    public void Error(LogSource source, string message) =>
        Log(LogLevel.Error, source, message);

    /// <inheritdoc/>
    public void Debug(LogSource source, string message) =>
        Log(LogLevel.Debug, source, message);
}
