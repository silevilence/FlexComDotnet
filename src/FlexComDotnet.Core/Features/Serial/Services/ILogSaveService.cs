namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 日志保存格式
/// </summary>
public enum LogSaveFormat
{
    /// <summary>
    /// 文本格式 (.txt)，包含时间戳和方向标识
    /// </summary>
    Text,

    /// <summary>
    /// 纯二进制格式 (.bin)，仅包含原始数据
    /// </summary>
    Binary,

    /// <summary>
    /// 带时间戳的二进制格式 (.bin)，每条记录包含时间戳
    /// </summary>
    BinaryWithTimestamp
}

/// <summary>
/// 日志保存选项
/// </summary>
public class LogSaveOptions
{
    /// <summary>
    /// 保存格式
    /// </summary>
    public LogSaveFormat Format { get; set; } = LogSaveFormat.Text;

    /// <summary>
    /// 是否包含发送数据
    /// </summary>
    public bool IncludeTx { get; set; } = true;

    /// <summary>
    /// 是否包含接收数据
    /// </summary>
    public bool IncludeRx { get; set; } = true;

    /// <summary>
    /// 是否使用 Hex 格式（仅对 Text 格式有效）
    /// </summary>
    public bool UseHexFormat { get; set; }
}

/// <summary>
/// 日志保存服务接口
/// </summary>
public interface ILogSaveService
{
    /// <summary>
    /// 保存日志到文件
    /// </summary>
    /// <param name="filePath">保存路径</param>
    /// <param name="records">数据记录列表</param>
    /// <param name="options">保存选项</param>
    /// <returns>是否保存成功</returns>
    bool Save(string filePath, IEnumerable<LogRecord> records, LogSaveOptions options);

    /// <summary>
    /// 异步保存日志到文件
    /// </summary>
    Task<bool> SaveAsync(string filePath, IEnumerable<LogRecord> records, LogSaveOptions options);

    /// <summary>
    /// 获取推荐的文件扩展名
    /// </summary>
    string GetRecommendedExtension(LogSaveFormat format);
}

/// <summary>
/// 日志记录
/// </summary>
public record LogRecord(byte[] Data, bool IsTx, DateTime Timestamp);
