using System.Text;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 日志保存服务实现
/// </summary>
public class LogSaveService : ILogSaveService
{
    /// <inheritdoc/>
    public bool Save(string filePath, IEnumerable<LogRecord> records, LogSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var filteredRecords = FilterRecords(records, options);

            return options.Format switch
            {
                LogSaveFormat.Text => SaveAsText(filePath, filteredRecords, options),
                LogSaveFormat.Binary => SaveAsBinary(filePath, filteredRecords),
                LogSaveFormat.BinaryWithTimestamp => SaveAsBinaryWithTimestamp(filePath, filteredRecords),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SaveAsync(string filePath, IEnumerable<LogRecord> records, LogSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var filteredRecords = FilterRecords(records, options).ToList();

            return options.Format switch
            {
                LogSaveFormat.Text => await SaveAsTextAsync(filePath, filteredRecords, options),
                LogSaveFormat.Binary => await SaveAsBinaryAsync(filePath, filteredRecords),
                LogSaveFormat.BinaryWithTimestamp => await SaveAsBinaryWithTimestampAsync(filePath, filteredRecords),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetRecommendedExtension(LogSaveFormat format)
    {
        return format switch
        {
            LogSaveFormat.Text => ".txt",
            LogSaveFormat.Binary => ".bin",
            LogSaveFormat.BinaryWithTimestamp => ".bin",
            _ => ".txt"
        };
    }

    private static IEnumerable<LogRecord> FilterRecords(IEnumerable<LogRecord> records, LogSaveOptions options)
    {
        return records.Where(r => (r.IsTx && options.IncludeTx) || (!r.IsTx && options.IncludeRx));
    }

    private static bool SaveAsText(string filePath, IEnumerable<LogRecord> records, LogSaveOptions options)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        
        foreach (var record in records)
        {
            var line = FormatTextLine(record, options);
            writer.WriteLine(line);
        }

        return true;
    }

    private static async Task<bool> SaveAsTextAsync(string filePath, IEnumerable<LogRecord> records, LogSaveOptions options)
    {
        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        foreach (var record in records)
        {
            var line = FormatTextLine(record, options);
            await writer.WriteLineAsync(line);
        }

        return true;
    }

    private static string FormatTextLine(LogRecord record, LogSaveOptions options)
    {
        var timestamp = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var direction = record.IsTx ? "TX" : "RX";
        var data = options.UseHexFormat
            ? HexHelper.BytesToHexString(record.Data)
            : HexHelper.BytesToAsciiString(record.Data, '.');

        return $"[{timestamp}] [{direction}] {data}";
    }

    private static bool SaveAsBinary(string filePath, IEnumerable<LogRecord> records)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

        foreach (var record in records)
        {
            stream.Write(record.Data, 0, record.Data.Length);
        }

        return true;
    }

    private static async Task<bool> SaveAsBinaryAsync(string filePath, IEnumerable<LogRecord> records)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

        foreach (var record in records)
        {
            await stream.WriteAsync(record.Data);
        }

        return true;
    }

    private static bool SaveAsBinaryWithTimestamp(string filePath, IEnumerable<LogRecord> records)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // 写入文件头标识
        writer.Write("FLEXCOM"u8.ToArray());
        writer.Write((byte)1); // 版本号

        foreach (var record in records)
        {
            WriteTimestampedRecord(writer, record);
        }

        return true;
    }

    private static async Task<bool> SaveAsBinaryWithTimestampAsync(string filePath, IEnumerable<LogRecord> records)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // 写入文件头标识
        writer.Write("FLEXCOM"u8.ToArray());
        writer.Write((byte)1); // 版本号

        foreach (var record in records)
        {
            WriteTimestampedRecord(writer, record);
        }

        return true;
    }

    private static void WriteTimestampedRecord(BinaryWriter writer, LogRecord record)
    {
        // 时间戳 (8 bytes - Unix ticks)
        writer.Write(record.Timestamp.Ticks);
        // 方向标识 (1 byte: 0=RX, 1=TX)
        writer.Write((byte)(record.IsTx ? 1 : 0));
        // 数据长度 (4 bytes)
        writer.Write(record.Data.Length);
        // 数据内容
        writer.Write(record.Data);
    }
}
