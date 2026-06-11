using System.Collections.ObjectModel;
using System.Text;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Serial.ViewModels;

/// <summary>
/// 数据记录类型
/// </summary>
public enum DataRecordType
{
    Normal,           // 普通数据
    HookProcessed,    // 被 Hook 处理过的数据
    ScriptAutoReply,  // 脚本自动应答
    AutoReply         // 规则自动回复
}

/// <summary>
/// 数据记录，用于存储原始数据以支持显示模式切换
/// </summary>
public record DataRecord(
    byte[] Data, 
    bool IsTx, 
    DateTime Timestamp, 
    DataRecordType RecordType = DataRecordType.Normal,
    byte[]? OriginalData = null);

/// <summary>
/// 串口收发通信 ViewModel
/// </summary>
public partial class SerialCommunicationViewModel : ObservableObject, IDisposable
{
    private readonly ISerialPortService _serialPortService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogSaveService _logSaveService;
    private readonly IScriptHookService? _scriptHookService;
    private readonly IAutoReplyService? _autoReplyService;
    private readonly List<DataRecord> _dataRecords = [];
    private readonly List<DataRecord> _pausedRecords = [];
    private readonly System.Timers.Timer _sendTimer;
    private readonly SynchronizationContext? _syncContext;
    private readonly HashSet<byte[]> _pendingAutoReplyData = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    #region 日志保存属性

    /// <summary>
    /// 日志保存格式
    /// </summary>
    [ObservableProperty]
    private LogSaveFormat _logSaveFormat = LogSaveFormat.Text;

    /// <summary>
    /// 日志保存是否包含发送数据
    /// </summary>
    [ObservableProperty]
    private bool _logIncludeTx = true;

    /// <summary>
    /// 日志保存是否包含接收数据
    /// </summary>
    [ObservableProperty]
    private bool _logIncludeRx = true;

    /// <summary>
    /// 日志保存是否使用 Hex 格式
    /// </summary>
    [ObservableProperty]
    private bool _logUseHexFormat;

    /// <summary>
    /// 日志保存状态
    /// </summary>
    [ObservableProperty]
    private string _logSaveStatus = string.Empty;

    #endregion

    #region 基础属性

    /// <summary>
    /// 接收到的数据
    /// </summary>
    [ObservableProperty]
    private string _receivedData = string.Empty;

    /// <summary>
    /// 按条显示的数据记录（供 ListBox 绑定）
    /// </summary>
    public ObservableCollection<DataRecord> DisplayRecords
    {
        get => _displayRecords;
        set => SetProperty(ref _displayRecords, value);
    }
    private ObservableCollection<DataRecord> _displayRecords = [];

    /// <summary>
    /// 待发送的文本
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _sendText = string.Empty;

    /// <summary>
    /// 是否使用 Hex 显示模式（接收区）
    /// </summary>
    [ObservableProperty]
    private bool _isHexDisplayMode;

    partial void OnIsHexDisplayModeChanged(bool value)
    {
        RecordDisplaySettings.IsHexDisplayMode = value;
        InvalidateDisplay();
        SaveDisplayConfig();
    }

    /// <summary>
    /// 是否使用 Hex 发送模式
    /// </summary>
    [ObservableProperty]
    private bool _isHexSendMode;

    partial void OnIsHexSendModeChanged(bool value)
    {
        // 切换发送模式时保存配置
        SaveDisplayConfig();
    }

    /// <summary>
    /// 发送状态信息
    /// </summary>
    [ObservableProperty]
    private string _sendStatus = string.Empty;

    /// <summary>
    /// 是否已连接（用于 UI 绑定）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isConnected;

    #endregion

    #region 数据统计属性

    /// <summary>
    /// 接收字节数
    /// </summary>
    [ObservableProperty]
    private long _rxBytes;

    /// <summary>
    /// 发送字节数
    /// </summary>
    [ObservableProperty]
    private long _txBytes;

    #endregion

    #region 显示选项属性

    /// <summary>
    /// 是否显示时间戳
    /// </summary>
    [ObservableProperty]
    private bool _showTimestamp;

    partial void OnShowTimestampChanged(bool value)
    {
        RecordDisplaySettings.ShowTimestamp = value;
        InvalidateDisplay();
        SaveDisplayConfig();
    }

    /// <summary>
    /// 时间戳是否显示日期
    /// </summary>
    [ObservableProperty]
    private bool _showDateInTimestamp;

    partial void OnShowDateInTimestampChanged(bool value)
    {
        RecordDisplaySettings.ShowDateInTimestamp = value;
        InvalidateDisplay();
        SaveDisplayConfig();
    }

    /// <summary>
    /// 是否自动换行
    /// </summary>
    [ObservableProperty]
    private bool _autoLineBreak = true;

    partial void OnAutoLineBreakChanged(bool value)
    {
        UpdateReceivedDataText();
        SaveDisplayConfig();
    }

    /// <summary>
    /// 是否暂停显示
    /// </summary>
    [ObservableProperty]
    private bool _isPaused;

    partial void OnIsPausedChanged(bool value)
    {
        if (!value)
        {
            // 恢复显示时，将缓冲区内容添加到显示区
            FlushPausedBuffer();
        }
    }

    #endregion

    #region 发送辅助选项属性

    /// <summary>
    /// 是否自动追加回车换行
    /// </summary>
    [ObservableProperty]
    private bool _appendCrLf;

    /// <summary>
    /// 校验和类型
    /// </summary>
    [ObservableProperty]
    private ChecksumType _appendChecksumType = ChecksumType.None;

    #endregion

    #region 定时发送属性

    private bool _isTimerEnabled;

    /// <summary>
    /// 是否启用定时发送
    /// </summary>
    public bool IsTimerEnabled
    {
        get => _isTimerEnabled;
        set
        {
            if (_isTimerEnabled == value)
            {
                return;
            }

            // 启用定时发送
            if (value)
            {
                if (CanSend())
                {
                    _sendTimer.Start();
                    SetProperty(ref _isTimerEnabled, true);
                }
                else
                {
                    // 无法发送时，强制设为 false（触发 UI 更新）
                    SetProperty(ref _isTimerEnabled, false);
                }
            }
            else
            {
                // 停止定时发送
                _sendTimer.Stop();
                SetProperty(ref _isTimerEnabled, false);
            }
        }
    }

    private int _timerInterval = 1000;
    
    /// <summary>
    /// 定时发送间隔 (ms)
    /// </summary>
    public int TimerInterval
    {
        get => _timerInterval;
        set
        {
            // 最小间隔限制为 10ms
            var newValue = Math.Max(10, value);
            if (SetProperty(ref _timerInterval, newValue))
            {
                _sendTimer.Interval = newValue;
            }
        }
    }

    #endregion

    public SerialCommunicationViewModel(ISerialPortService serialPortService, IConfigurationService configurationService, ILogSaveService logSaveService, IScriptHookService? scriptHookService = null, IAutoReplyService? autoReplyService = null)
    {
        _serialPortService = serialPortService;
        _configurationService = configurationService;
        _logSaveService = logSaveService;
        _scriptHookService = scriptHookService;
        _autoReplyService = autoReplyService;

        // 捕获 UI 线程的同步上下文，用于跨线程更新 UI
        _syncContext = SynchronizationContext.Current;

        // 初始化定时器
        _sendTimer = new System.Timers.Timer(TimerInterval);
        _sendTimer.Elapsed += OnTimerElapsed;
        _sendTimer.AutoReset = true;

        // 订阅数据接收事件
        _serialPortService.FrameReceived += OnFrameReceived;
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;
        _serialPortService.HookProcessed += OnHookProcessed;

        // 订阅脚本自动应答事件
        if (_scriptHookService != null)
        {
            _scriptHookService.AutoReplySent += OnAutoReplySent;
        }

        // 订阅规则自动回复事件
        if (_autoReplyService != null)
        {
            _autoReplyService.ReplyTriggered += OnAutoReplyTriggered;
        }

        // 初始化连接状态
        IsConnected = _serialPortService.IsConnected;

        // 加载显示配置
        LoadDisplayConfig();
    }

    /// <summary>
    /// 加载显示配置
    /// </summary>
    private void LoadDisplayConfig()
    {
        var config = _configurationService.Load();
        
        // 使用字段直接赋值，避免触发 OnChanged 回调导致保存
        // 抑制 MVVMTK0034 警告，因为这里故意直接访问字段
#pragma warning disable MVVMTK0034
        _isHexDisplayMode = config.DisplayConfig.IsHexDisplayMode;
        _showTimestamp = config.DisplayConfig.ShowTimestamp;
        _showDateInTimestamp = config.DisplayConfig.ShowDateInTimestamp;
        _autoLineBreak = config.DisplayConfig.AutoLineBreak;
        _isHexSendMode = config.DisplayConfig.IsHexSendMode;
#pragma warning restore MVVMTK0034
        
        // 通知属性变化
        OnPropertyChanged(nameof(IsHexDisplayMode));
        OnPropertyChanged(nameof(ShowTimestamp));
        OnPropertyChanged(nameof(ShowDateInTimestamp));
        OnPropertyChanged(nameof(AutoLineBreak));
        OnPropertyChanged(nameof(IsHexSendMode));
        
        // 同步静态显示设置（OnChanged 不会被触发，因为直接赋字段）
        RecordDisplaySettings.IsHexDisplayMode = _isHexDisplayMode;
        RecordDisplaySettings.ShowTimestamp = _showTimestamp;
        RecordDisplaySettings.ShowDateInTimestamp = _showDateInTimestamp;
        OnPropertyChanged(nameof(ShowDateInTimestamp));
        OnPropertyChanged(nameof(AutoLineBreak));
        OnPropertyChanged(nameof(IsHexSendMode));
    }

    /// <summary>
    /// 保存显示配置
    /// </summary>
    private void SaveDisplayConfig()
    {
        var config = _configurationService.Load();
        config.DisplayConfig.IsHexDisplayMode = IsHexDisplayMode;
        config.DisplayConfig.ShowTimestamp = ShowTimestamp;
        config.DisplayConfig.ShowDateInTimestamp = ShowDateInTimestamp;
        config.DisplayConfig.AutoLineBreak = AutoLineBreak;
        config.DisplayConfig.IsHexSendMode = IsHexSendMode;
        _configurationService.Save(config);
    }

    /// <summary>
    /// 强制切换到 Hex 模式并转换现有内容
    /// </summary>
    public void SwitchToHexModeWithConversion()
    {
        if (IsHexSendMode)
        {
            // 已经是 Hex 模式，无需切换
            return;
        }

        // 转换当前 ASCII 内容为 Hex
        if (!string.IsNullOrEmpty(SendText))
        {
            var bytes = Encoding.ASCII.GetBytes(SendText);
            SendText = HexHelper.BytesToHexString(bytes);
        }

        // 切换到 Hex 模式
        IsHexSendMode = true;
    }

    /// <summary>
    /// 发送数据命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        if (string.IsNullOrEmpty(SendText))
        {
            return;
        }

        byte[] data;
        if (IsHexSendMode)
        {
            // Hex 模式发送
            if (!HexHelper.IsValidHexString(SendText))
            {
                SendStatus = "发送失败: 无效的十六进制格式";
                return;
            }
            data = HexHelper.HexStringToBytes(SendText);
            if (data.Length == 0 && !string.IsNullOrWhiteSpace(SendText))
            {
                SendStatus = "发送失败: 无效的十六进制格式";
                return;
            }
        }
        else
        {
            // ASCII 模式发送
            data = HexHelper.AsciiStringToBytes(SendText);
        }

        if (data.Length == 0)
        {
            return;
        }

        // 追加回车换行
        if (AppendCrLf && !IsHexSendMode)
        {
            data = [.. data, 0x0D, 0x0A];
        }

        // 追加校验和
        if (AppendChecksumType != ChecksumType.None)
        {
            data = ChecksumHelper.AppendChecksum(data, AppendChecksumType);
        }

        var success = _serialPortService.Send(data);
        if (success)
        {
            // 更新发送字节计数
            TxBytes += data.Length;
            
            // 将发送的数据显示到接收区（如果有 TxPostProcessor，由 HookProcessed 事件处理）
            if (_serialPortService.TxPostProcessor == null)
            {
                AddDataRecord(data, isTx: true);
            }
            SendStatus = $"发送成功: {data.Length} 字节";
        }
        else
        {
            SendStatus = "发送失败";
        }
    }

    private bool CanSend() => IsConnected && !string.IsNullOrEmpty(SendText);

    /// <summary>
    /// 清空接收区命令
    /// </summary>
    [RelayCommand]
    private void ClearReceived()
    {
        _dataRecords.Clear();
        _pausedRecords.Clear();
        ReceivedData = string.Empty;
        DisplayRecords.Clear();
    }

    /// <summary>
    /// 清空发送区命令
    /// </summary>
    [RelayCommand]
    private void ClearSend()
    {
        SendText = string.Empty;
        SendStatus = string.Empty;
    }

    /// <summary>
    /// 重置计数器命令
    /// </summary>
    [RelayCommand]
    private void ResetCounters()
    {
        RxBytes = 0;
        TxBytes = 0;
    }

    /// <summary>
    /// 切换暂停/继续命令
    /// </summary>
    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // 使用同步上下文将发送操作调度到 UI 线程
        if (_syncContext != null)
        {
            _syncContext.Post(_ => ExecuteTimerSend(), null);
        }
        else
        {
            ExecuteTimerSend();
        }
    }

    /// <summary>
    /// 执行定时发送（必须在 UI 线程执行）
    /// </summary>
    private void ExecuteTimerSend()
    {
        if (CanSend())
        {
            Send();
        }
        else
        {
            // 如果无法发送，停止定时器
            _sendTimer.Stop();
            IsTimerEnabled = false;
        }
    }

    /// <summary>
    /// 添加数据记录并更新显示
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="isTx">是否为发送数据</param>
    /// <param name="recordType">记录类型</param>
    /// <param name="originalData">原始数据（Hook处理前）</param>
    private void AddDataRecord(byte[] data, bool isTx, DataRecordType recordType = DataRecordType.Normal, byte[]? originalData = null)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        var record = new DataRecord(data, isTx, DateTime.Now, recordType, originalData);

        if (IsPaused)
        {
            _pausedRecords.Add(record);
        }
        else
        {
            _dataRecords.Add(record);
            DisplayRecords.Add(record);
            UpdateReceivedDataText();

            // 限制记录总数，防止内存无限增长
            if (_dataRecords.Count > 10000)
            {
                _dataRecords.RemoveAt(0);
                _pausedRecords.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 更新 ReceivedData 全文（仅用于搜索/复制等场景）
    /// </summary>
    private void UpdateReceivedDataText()
    {
        if (_dataRecords.Count == 0)
        {
            ReceivedData = string.Empty;
            return;
        }

        // 只保留最新 200 条用于全文显示，避免字符串过大
        var start = Math.Max(0, _dataRecords.Count - 200);
        var sb = new StringBuilder();
        for (int i = start; i < _dataRecords.Count; i++)
        {
            var record = _dataRecords[i];
            var prefix = record.IsTx ? "[TX] " : "[RX] ";
            if (record.RecordType == DataRecordType.ScriptAutoReply)
                prefix = "[⚡]";
            else if (record.RecordType == DataRecordType.AutoReply)
                prefix = "[↩️]";

            // 时间戳
            if (ShowTimestamp)
            {
                var format = ShowDateInTimestamp ? "yyyy-MM-dd HH:mm:ss.fff" : "HH:mm:ss.fff";
                prefix = $"[{record.Timestamp.ToString(format)}] {prefix}";
            }

            sb.Append(prefix);
            sb.Append(IsHexDisplayMode
                ? HexHelper.BytesToHexString(record.Data)
                : HexHelper.BytesToAsciiString(record.Data, '.'));

            if (AutoLineBreak)
                sb.AppendLine();
        }
        ReceivedData = sb.ToString();
    }

    /// <summary>
    /// 添加脚本自动应答记录（公开方法，供外部调用）
    /// </summary>
    public void AddScriptAutoReplyRecord(byte[] data)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => AddDataRecord(data, isTx: true, DataRecordType.ScriptAutoReply), null);
        }
        else
        {
            AddDataRecord(data, isTx: true, DataRecordType.ScriptAutoReply);
        }
    }

    /// <summary>
    /// 添加 Hook 处理后的记录（公开方法，供外部调用）
    /// </summary>
    public void AddHookProcessedRecord(byte[] processedData, byte[] originalData, bool isTx)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => AddDataRecord(processedData, isTx, DataRecordType.HookProcessed, originalData), null);
        }
        else
        {
            AddDataRecord(processedData, isTx, DataRecordType.HookProcessed, originalData);
        }
    }

    /// <summary>
    /// 格式化单条数据记录
    /// </summary>
    private string FormatRecord(DataRecord record)
    {
        var prefix = record.IsTx ? "[TX] " : "[RX] ";

        // 脚本自动应答特殊标记
        if (record.RecordType == DataRecordType.ScriptAutoReply)
        {
            prefix = "[⚡]";
        }
        // 规则自动回复特殊标记
        else if (record.RecordType == DataRecordType.AutoReply)
        {
            prefix = "[↩️]";
        }

        // 添加时间戳
        if (ShowTimestamp)
        {
            var format = ShowDateInTimestamp ? "yyyy-MM-dd HH:mm:ss.fff" : "HH:mm:ss.fff";
            var timestamp = record.Timestamp.ToString(format);
            prefix = $"[{timestamp}] {prefix}";
        }

        string dataStr;
        if (IsHexDisplayMode)
        {
            dataStr = HexHelper.BytesToHexString(record.Data);
        }
        else
        {
            dataStr = HexHelper.BytesToAsciiString(record.Data, '.');
        }

        // 如果数据被 Hook 处理过或是脚本/规则自动应答，且数据有变化，显示原始数据和处理后数据
        if ((record.RecordType == DataRecordType.HookProcessed || record.RecordType == DataRecordType.ScriptAutoReply || record.RecordType == DataRecordType.AutoReply) 
            && record.OriginalData != null 
            && !record.Data.SequenceEqual(record.OriginalData))
        {
            string originalStr;
            if (IsHexDisplayMode)
            {
                originalStr = HexHelper.BytesToHexString(record.OriginalData);
            }
            else
            {
                originalStr = HexHelper.BytesToAsciiString(record.OriginalData, '.');
            }

            return prefix + $"⬇ {originalStr} ➡ ⬆ {dataStr}";
        }

        return prefix + dataStr;
    }

    /// <summary>
    /// 使当前显示失效，触发 ListBox 重新调用转换器格式化可见项
    /// </summary>
    private void InvalidateDisplay()
    {
        // 重新赋值集合，触发 ListBox 刷新可见项的绑定
        DisplayRecords = new ObservableCollection<DataRecord>(_displayRecords);
        // 同步更新全文文本（仅最新 200 条，性能可接受）
        UpdateReceivedDataText();
    }

    /// <summary>
    /// 将暂停期间缓冲的数据刷新到显示区
    /// </summary>
    private void FlushPausedBuffer()
    {
        if (_pausedRecords.Count > 0)
        {
            _dataRecords.AddRange(_pausedRecords);
            _pausedRecords.Clear();
            InvalidateDisplay();
        }
    }

    /// <summary>
    /// Hook 处理完成事件处理
    /// </summary>
    private void OnHookProcessed(object? sender, HookProcessedEventArgs e)
    {
        // 如果是自动应答触发的 Tx Hook，跳过（由 OnAutoReplySent 统一处理）
        if (e.IsTx && _pendingAutoReplyData.Remove(e.OriginalData))
        {
            return;
        }
        
        if (_syncContext != null)
        {
            _syncContext.Post(_ => AddDataRecord(e.ProcessedData, e.IsTx, DataRecordType.HookProcessed, e.OriginalData), null);
        }
        else
        {
            AddDataRecord(e.ProcessedData, e.IsTx, DataRecordType.HookProcessed, e.OriginalData);
        }
    }

    /// <summary>
    /// 脚本自动应答事件处理
    /// </summary>
    private void OnAutoReplySent(object? sender, ScriptAutoReplyEventArgs e)
    {
        // 标记此数据为自动应答，让 OnHookProcessed 跳过
        _pendingAutoReplyData.Add(e.ReplyData);
        
        // 使用处理后的数据作为实际发送数据，原始回复数据作为 OriginalData
        if (_syncContext != null)
        {
            _syncContext.Post(_ => AddDataRecord(e.ProcessedReplyData, isTx: true, DataRecordType.ScriptAutoReply, e.ReplyData), null);
        }
        else
        {
            AddDataRecord(e.ProcessedReplyData, isTx: true, DataRecordType.ScriptAutoReply, e.ReplyData);
        }
    }

    /// <summary>
    /// 规则自动回复事件处理
    /// </summary>
    private void OnAutoReplyTriggered(object? sender, ReplyEventArgs e)
    {
        // AutoReplyService 的调用顺序: Send() → HookProcessed → ReplyTriggered
        // 因此 OnHookProcessed 可能已经添加了一条 HookProcessed 记录
        // 需要找到它并替换为 AutoReply 类型
        if (_syncContext != null)
        {
            _syncContext.Post(_ => ProcessAutoReplyRecord(e), null);
        }
        else
        {
            ProcessAutoReplyRecord(e);
        }
    }

    /// <summary>
    /// 处理规则自动回复记录（查找并替换 Hook 记录，或新增）
    /// </summary>
    private void ProcessAutoReplyRecord(ReplyEventArgs e)
    {
        // 查找最近的 HookProcessed TX 记录，其 OriginalData 匹配此回复数据
        var records = IsPaused ? _pausedRecords : _dataRecords;
        for (int i = records.Count - 1; i >= Math.Max(0, records.Count - 10); i--)
        {
            var record = records[i];
            if (record.IsTx && record.RecordType == DataRecordType.HookProcessed
                && record.OriginalData != null
                && record.OriginalData.SequenceEqual(e.ReplyData))
            {
                // 将 HookProcessed 记录替换为 AutoReply 类型，保留处理后的数据和原始数据
                records[i] = record with { RecordType = DataRecordType.AutoReply };
                if (!IsPaused) InvalidateDisplay();
                return;
            }
        }

        // 没有 Hook 处理过的记录，直接添加
        AddDataRecord(e.ReplyData, isTx: true, DataRecordType.AutoReply);
    }

    /// <summary>
    /// 数据接收处理
    /// </summary>
    private void OnFrameReceived(object? sender, byte[] frame)
    {
        // 使用同步上下文将 UI 更新调度到 UI 线程
        if (_syncContext != null)
        {
            _syncContext.Post(_ => ProcessReceivedData(frame), null);
        }
        else
        {
            // 如果没有同步上下文（如单元测试），直接执行
            ProcessReceivedData(frame);
        }
    }

    /// <summary>
    /// 处理接收到的数据（必须在 UI 线程执行）
    /// </summary>
    private void ProcessReceivedData(byte[] data)
    {
        // 更新接收字节计数
        RxBytes += data.Length;
        
        AddDataRecord(data, isTx: false);
    }

    /// <summary>
    /// 获取当前数据记录列表（用于日志保存）
    /// </summary>
    public IReadOnlyList<DataRecord> GetDataRecords() => _dataRecords.AsReadOnly();

    /// <summary>
    /// 根据索引获取数据记录（用于协议解析等功能）
    /// </summary>
    public DataRecord? GetDataRecord(int index) =>
        index >= 0 && index < _dataRecords.Count ? _dataRecords[index] : null;

    /// <summary>
    /// 保存日志到指定路径
    /// </summary>
    /// <param name="filePath">保存路径</param>
    /// <returns>是否保存成功</returns>
    public bool SaveLog(string filePath)
    {
        var options = new LogSaveOptions
        {
            Format = LogSaveFormat,
            IncludeTx = LogIncludeTx,
            IncludeRx = LogIncludeRx,
            UseHexFormat = IsHexDisplayMode
        };

        var records = _dataRecords.Select(r => new LogRecord(r.Data, r.IsTx, r.Timestamp));
        var result = _logSaveService.Save(filePath, records, options);

        LogSaveStatus = result
            ? $"日志已保存: {Path.GetFileName(filePath)}"
            : "日志保存失败";

        return result;
    }

    /// <summary>
    /// 获取推荐的日志文件扩展名
    /// </summary>
    public string GetRecommendedLogExtension() => _logSaveService.GetRecommendedExtension(LogSaveFormat);

    /// <summary>
    /// 发送外部数据（由指令列表调用）
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <returns>是否发送成功</returns>
    public bool SendData(byte[] data)
    {
        if (!IsConnected || data == null || data.Length == 0)
        {
            return false;
        }

        var success = _serialPortService.Send(data);
        if (success)
        {
            TxBytes += data.Length;
            if (_serialPortService.TxPostProcessor == null)
            {
                AddDataRecord(data, isTx: true);
            }
        }

        return success;
    }

    /// <summary>
    /// 连接状态变化处理
    /// </summary>
    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        
        // 断开连接时停止定时器
        if (!connected && IsTimerEnabled)
        {
            _sendTimer.Stop();
            IsTimerEnabled = false;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _sendTimer.Stop();
                _sendTimer.Elapsed -= OnTimerElapsed;
                _sendTimer.Dispose();

                _serialPortService.FrameReceived -= OnFrameReceived;
                _serialPortService.ConnectionStateChanged -= OnConnectionStateChanged;
                _serialPortService.HookProcessed -= OnHookProcessed;

                if (_scriptHookService != null)
                {
                    _scriptHookService.AutoReplySent -= OnAutoReplySent;
                }

                if (_autoReplyService != null)
                {
                    _autoReplyService.ReplyTriggered -= OnAutoReplyTriggered;
                }
            }

            _disposed = true;
        }
    }
}