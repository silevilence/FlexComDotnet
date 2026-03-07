using System.Text;
using FlexComDotnet.Core.Features.Logging.Models;

namespace FlexComDotnet.Core.Features.Logging.Services;

/// <summary>
/// 日志持久化服务 - 按日期分文件存储
/// </summary>
public class LogPersistenceService : ILogPersistenceService
{
    private readonly string _logDirectory;
    private StreamWriter? _writer;
    private string? _currentDate;
    private readonly object _lock = new();
    private bool _disposed;

    public LogPersistenceService(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    /// <inheritdoc/>
    public void WriteSessionStart()
    {
        var line = $"\n=== FlexComDotnet Session Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===";
        WriteLine(line);
    }

    /// <inheritdoc/>
    public void WriteSessionEnd()
    {
        var line = $"=== FlexComDotnet Session End: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
        WriteLine(line);
    }

    /// <inheritdoc/>
    public void Write(LogEntry entry)
    {
        var levelStr = entry.Level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            _ => "INFO"
        };

        var sourceStr = GetSourceDisplayName(entry.Source);
        var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] [{sourceStr}] {entry.Message}";
        WriteLine(line);
    }

    /// <inheritdoc/>
    public void Flush()
    {
        lock (_lock)
        {
            _writer?.Flush();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            EnsureWriter();
            _writer!.WriteLine(line);
        }
    }

    private void EnsureWriter()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        if (_currentDate != today || _writer == null)
        {
            _writer?.Flush();
            _writer?.Dispose();

            var filePath = Path.Combine(_logDirectory, $"{today}.log");
            var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(fileStream, Encoding.UTF8);
            _currentDate = today;
        }
    }

    /// <summary>
    /// 获取日志来源的中文显示名
    /// </summary>
    internal static string GetSourceDisplayName(LogSource source) => source switch
    {
        LogSource.System => "系统",
        LogSource.Serial => "串口",
        LogSource.Network => "网络",
        LogSource.Script => "脚本",
        LogSource.AutoReply => "自动回复",
        LogSource.Protocol => "协议",
        LogSource.Visualization => "可视化",
        _ => source.ToString()
    };
}
