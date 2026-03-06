using System.Globalization;
using System.Text;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Visualization.Models;

namespace FlexComDotnet.Core.Features.Visualization.Services;

/// <summary>
/// 数据可视化服务实现
/// </summary>
public class VisualizationService : IVisualizationService
{
    private readonly IProtocolParserService _protocolParserService;
    private readonly Dictionary<string, ChannelConfig> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ChartDataPoint>> _channelData = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private int _maxDataPoints = 1000;

    /// <summary>
    /// 原始数据接收缓冲区，用于帧提取
    /// </summary>
    private byte[] _buffer = [];

    /// <summary>
    /// 采集期间已接收的总字节数
    /// </summary>
    private long _totalBytesReceived;

    /// <summary>
    /// 采集期间已成功提取的帧数
    /// </summary>
    private long _totalFramesExtracted;

    /// <summary>
    /// 是否已发送过提取失败通知
    /// </summary>
    private bool _extractionFailureNotified;

    public VisualizationService(IProtocolParserService protocolParserService)
    {
        _protocolParserService = protocolParserService ?? throw new ArgumentNullException(nameof(protocolParserService));
    }

    public bool IsRunning { get; private set; }

    public int MaxDataPoints
    {
        get => _maxDataPoints;
        set => _maxDataPoints = Math.Max(10, value);
    }

    public string? SelectedParserName { get; set; }

    public event EventHandler<DataPointAddedEventArgs>? DataPointAdded;
    public event EventHandler? DataCleared;
    public event EventHandler<VisualizationStateChangedEventArgs>? StateChanged;
    public event EventHandler<ExtractionFailedEventArgs>? ExtractionFailed;

    #region 通道管理

    public void AddChannel(ChannelConfig channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (string.IsNullOrWhiteSpace(channel.Id))
            throw new ArgumentException("通道 ID 不能为空", nameof(channel));

        lock (_lock)
        {
            if (_channels.ContainsKey(channel.Id))
                throw new ArgumentException($"通道 ID '{channel.Id}' 已存在", nameof(channel));

            _channels[channel.Id] = channel;
            _channelData[channel.Id] = [];
        }
    }

    public bool RemoveChannel(string channelId)
    {
        lock (_lock)
        {
            if (_channels.Remove(channelId))
            {
                _channelData.Remove(channelId);
                return true;
            }
            return false;
        }
    }

    public void UpdateChannel(ChannelConfig channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        lock (_lock)
        {
            if (!_channels.ContainsKey(channel.Id))
                throw new ArgumentException($"通道 ID '{channel.Id}' 不存在", nameof(channel));

            _channels[channel.Id] = channel;
        }
    }

    public IReadOnlyList<ChannelConfig> GetChannels()
    {
        lock (_lock)
        {
            return _channels.Values.OrderBy(c => c.Order).ToList();
        }
    }

    public ChannelConfig? GetChannel(string channelId)
    {
        lock (_lock)
        {
            return _channels.GetValueOrDefault(channelId);
        }
    }

    #endregion

    #region 数据管理

    public void PushData(ParsedFrame frame)
    {
        if (!IsRunning)
            return;

        if (frame == null || !frame.IsValid || frame.Fields.Count == 0)
            return;

        lock (_lock)
        {
            foreach (var channel in _channels.Values)
            {
                if (!channel.IsVisible)
                    continue;

                var field = frame.Fields.Find(f =>
                    f.Name.Equals(channel.FieldName, StringComparison.OrdinalIgnoreCase));

                if (field?.Value == null)
                    continue;

                if (!TryConvertToDouble(field.Value, out var value))
                    continue;

                var dataPoint = new ChartDataPoint(channel.Id, value, DateTime.Now);

                if (!_channelData.TryGetValue(channel.Id, out var dataList))
                {
                    dataList = [];
                    _channelData[channel.Id] = dataList;
                }

                dataList.Add(dataPoint);

                // 超出最大数据点数时，移除最早的数据
                while (dataList.Count > _maxDataPoints)
                {
                    dataList.RemoveAt(0);
                }

                DataPointAdded?.Invoke(this, new DataPointAddedEventArgs(dataPoint));
            }
        }
    }

    public void FeedRawData(byte[] data)
    {
        if (!IsRunning || data == null || data.Length == 0)
            return;

        if (string.IsNullOrEmpty(SelectedParserName))
            return;

        var parser = _protocolParserService.GetParser(SelectedParserName);
        if (parser == null)
            return;

        _totalBytesReceived += data.Length;

        // 将新数据追加到缓冲区
        var newBuffer = new byte[_buffer.Length + data.Length];
        _buffer.CopyTo(newBuffer, 0);
        data.CopyTo(newBuffer, _buffer.Length);
        _buffer = newBuffer;

        // 循环提取完整帧
        int frameCount = 0;
        while (_buffer.Length > 0)
        {
            try
            {
                if (!parser.TryExtractFrame(_buffer, out var frame, out var consumedBytes))
                    break;

                if (consumedBytes <= 0)
                    break;

                // 解析提取到的帧
                try
                {
                    var parsedFrame = parser.Parse(frame);
                    if (parsedFrame != null)
                    {
                        PushData(parsedFrame);
                        frameCount++;
                    }
                }
                catch
                {
                    // 解析出错时跳过该帧
                }

                // 移除已消耗的字节
                if (consumedBytes >= _buffer.Length)
                {
                    _buffer = [];
                }
                else
                {
                    _buffer = _buffer[consumedBytes..];
                }
            }
            catch
            {
                // 帧提取出错时清空缓冲区避免死循环
                _buffer = [];
                break;
            }
        }

        _totalFramesExtracted += frameCount;

        // 接收了足够数据（≥50字节）但从未提取到帧时，通知提取失败
        if (frameCount == 0 && !_extractionFailureNotified && _totalBytesReceived >= 50 && _totalFramesExtracted == 0)
        {
            _extractionFailureNotified = true;
            ExtractionFailed?.Invoke(this, new ExtractionFailedEventArgs(_totalBytesReceived, _totalFramesExtracted));
        }

        // 防止缓冲区无限增长（超过 64KB 时截断保留后半部分）
        if (_buffer.Length > 65536)
        {
            _buffer = _buffer[(_buffer.Length - 32768)..];
        }
    }

    public IReadOnlyList<ChartDataPoint> GetChannelData(string channelId)
    {
        lock (_lock)
        {
            if (_channelData.TryGetValue(channelId, out var data))
                return data.ToList();
            return [];
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<ChartDataPoint>> GetAllData()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, IReadOnlyList<ChartDataPoint>>();
            foreach (var kvp in _channelData)
            {
                result[kvp.Key] = kvp.Value.ToList();
            }
            return result;
        }
    }

    public void ClearData()
    {
        lock (_lock)
        {
            foreach (var dataList in _channelData.Values)
            {
                dataList.Clear();
            }
        }

        DataCleared?.Invoke(this, EventArgs.Empty);
    }

    public void ClearChannelData(string channelId)
    {
        lock (_lock)
        {
            if (_channelData.TryGetValue(channelId, out var data))
            {
                data.Clear();
            }
        }
    }

    #endregion

    #region 状态管理

    public void Start()
    {
        if (IsRunning)
            return;

        // 清空帧提取缓冲区和统计计数器
        _buffer = [];
        _totalBytesReceived = 0;
        _totalFramesExtracted = 0;
        _extractionFailureNotified = false;

        IsRunning = true;
        StateChanged?.Invoke(this, new VisualizationStateChangedEventArgs(true));
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        _buffer = [];
        _totalBytesReceived = 0;
        _totalFramesExtracted = 0;
        _extractionFailureNotified = false;
        StateChanged?.Invoke(this, new VisualizationStateChangedEventArgs(false));
    }

    #endregion

    #region 数据导出

    public void ExportToCsv(string filePath)
    {
        lock (_lock)
        {
            var sb = new StringBuilder();

            // 获取有数据的通道
            var channelsWithData = _channels.Values
                .Where(c => _channelData.ContainsKey(c.Id) && _channelData[c.Id].Count > 0)
                .OrderBy(c => c.Order)
                .ToList();

            if (channelsWithData.Count == 0)
            {
                // 写入空的 CSV 头
                sb.AppendLine("Timestamp");
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return;
            }

            // 写入表头: Timestamp, Channel1, Channel2, ...
            sb.Append("Timestamp");
            foreach (var channel in channelsWithData)
            {
                sb.Append(',');
                sb.Append(EscapeCsvField(channel.DisplayName));
            }
            sb.AppendLine();

            // 收集所有时间戳并排序
            var allTimestamps = channelsWithData
                .SelectMany(c => _channelData[c.Id].Select(dp => dp.Timestamp))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // 按时间戳逐行写入
            foreach (var timestamp in allTimestamps)
            {
                sb.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));

                foreach (var channel in channelsWithData)
                {
                    sb.Append(',');
                    var dataPoint = _channelData[channel.Id]
                        .Find(dp => dp.Timestamp == timestamp);
                    if (dataPoint != null)
                    {
                        sb.Append(dataPoint.Value.ToString(CultureInfo.InvariantCulture));
                    }
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 尝试将解析字段的值转换为 double
    /// </summary>
    private static bool TryConvertToDouble(object value, out double result)
    {
        result = 0;

        try
        {
            result = value switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                short s => s,
                byte b => b,
                uint ui => ui,
                ulong ul => ul,
                ushort us => us,
                sbyte sb => sb,
                decimal dec => (double)dec,
                string str when double.TryParse(str, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 转义 CSV 字段
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    #endregion
}
