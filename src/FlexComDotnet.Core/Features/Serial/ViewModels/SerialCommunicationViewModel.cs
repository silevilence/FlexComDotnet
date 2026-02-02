using System.Text;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Serial.ViewModels;

/// <summary>
/// 数据记录，用于存储原始数据以支持显示模式切换
/// </summary>
public record DataRecord(byte[] Data, bool IsTx, DateTime Timestamp);

/// <summary>
/// 串口收发通信 ViewModel
/// </summary>
public partial class SerialCommunicationViewModel : ObservableObject, IDisposable
{
    private readonly ISerialPortService _serialPortService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogSaveService _logSaveService;
    private readonly List<DataRecord> _dataRecords = [];
    private readonly List<DataRecord> _pausedRecords = [];
    private readonly System.Timers.Timer _sendTimer;
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
    /// 待发送的文本
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleTimerCommand))]
    private string _sendText = string.Empty;

    /// <summary>
    /// 是否使用 Hex 显示模式（接收区）
    /// </summary>
    [ObservableProperty]
    private bool _isHexDisplayMode;

    partial void OnIsHexDisplayModeChanged(bool value)
    {
        // 切换显示模式时刷新显示并保存配置
        RefreshDisplay();
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
    [NotifyCanExecuteChangedFor(nameof(ToggleTimerCommand))]
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
        // 切换时间戳显示时刷新显示并保存配置
        RefreshDisplay();
        SaveDisplayConfig();
    }

    /// <summary>
    /// 是否自动换行
    /// </summary>
    [ObservableProperty]
    private bool _autoLineBreak = true;

    partial void OnAutoLineBreakChanged(bool value)
    {
        // 切换自动换行时刷新显示并保存配置
        RefreshDisplay();
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

    /// <summary>
    /// 是否启用定时发送
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleTimerCommand))]
    private bool _isTimerEnabled;

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

    public SerialCommunicationViewModel(ISerialPortService serialPortService, IConfigurationService configurationService, ILogSaveService logSaveService)
    {
        _serialPortService = serialPortService;
        _configurationService = configurationService;
        _logSaveService = logSaveService;

        // 初始化定时器
        _sendTimer = new System.Timers.Timer(TimerInterval);
        _sendTimer.Elapsed += OnTimerElapsed;
        _sendTimer.AutoReset = true;

        // 订阅数据接收事件
        _serialPortService.DataReceived += OnDataReceived;
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;

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
        _autoLineBreak = config.DisplayConfig.AutoLineBreak;
        _isHexSendMode = config.DisplayConfig.IsHexSendMode;
#pragma warning restore MVVMTK0034
        
        // 通知属性变化
        OnPropertyChanged(nameof(IsHexDisplayMode));
        OnPropertyChanged(nameof(ShowTimestamp));
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
        config.DisplayConfig.AutoLineBreak = AutoLineBreak;
        config.DisplayConfig.IsHexSendMode = IsHexSendMode;
        _configurationService.Save(config);
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
            
            // 将发送的数据显示到接收区
            AddDataRecord(data, isTx: true);
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

    /// <summary>
    /// 切换定时发送命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleTimer))]
    private void ToggleTimer()
    {
        if (IsTimerEnabled)
        {
            _sendTimer.Stop();
            IsTimerEnabled = false;
        }
        else
        {
            if (CanSend())
            {
                _sendTimer.Start();
                IsTimerEnabled = true;
            }
        }
    }

    private bool CanToggleTimer() => IsConnected && !string.IsNullOrEmpty(SendText) || IsTimerEnabled;

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
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
    private void AddDataRecord(byte[] data, bool isTx)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        var record = new DataRecord(data, isTx, DateTime.Now);

        if (IsPaused)
        {
            // 暂停时缓冲记录
            _pausedRecords.Add(record);
        }
        else
        {
            // 正常添加记录并刷新显示
            _dataRecords.Add(record);
            RefreshDisplay();
        }
    }

    /// <summary>
    /// 格式化单条数据记录
    /// </summary>
    private string FormatRecord(DataRecord record)
    {
        var prefix = record.IsTx ? "[TX] " : "[RX] ";

        // 添加时间戳
        if (ShowTimestamp)
        {
            var timestamp = record.Timestamp.ToString("HH:mm:ss.fff");
            prefix = $"[{timestamp}] {prefix}";
        }

        if (IsHexDisplayMode)
        {
            return prefix + HexHelper.BytesToHexString(record.Data);
        }
        else
        {
            return prefix + HexHelper.BytesToAsciiString(record.Data, '.');
        }
    }

    /// <summary>
    /// 刷新显示区域（根据当前设置重新格式化所有数据）
    /// </summary>
    private void RefreshDisplay()
    {
        if (_dataRecords.Count == 0)
        {
            ReceivedData = string.Empty;
            return;
        }

        var sb = new StringBuilder();
        
        foreach (var record in _dataRecords)
        {
            var formatted = FormatRecord(record);
            if (AutoLineBreak)
            {
                sb.AppendLine(formatted);
            }
            else
            {
                sb.Append(formatted);
            }
        }

        ReceivedData = sb.ToString();
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
            RefreshDisplay();
        }
    }

    /// <summary>
    /// 数据接收处理
    /// </summary>
    private void OnDataReceived(object? sender, byte[] data)
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
            UseHexFormat = LogUseHexFormat
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
            AddDataRecord(data, isTx: true);
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

                _serialPortService.DataReceived -= OnDataReceived;
                _serialPortService.ConnectionStateChanged -= OnConnectionStateChanged;
            }

            _disposed = true;
        }
    }
}