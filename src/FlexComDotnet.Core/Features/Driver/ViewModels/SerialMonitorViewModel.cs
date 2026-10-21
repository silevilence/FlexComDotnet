using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Driver.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Driver.ViewModels;

public sealed partial class SerialMonitorRecordViewModel : ObservableObject
{
    private readonly string _hexContent;
    private readonly string _textContent;
    private readonly string? _controlContent;

    public DateTime Timestamp { get; }
    public SerialMonitorDataDirection Direction { get; }
    public SerialMonitorDataRecord Record { get; }
    public string EventLabel { get; }
    public string RawHex => _hexContent;
    public string RawDetails =>
        $"{(Record.IsFrame ? "定长重组帧（非单个原始 I/O 记录）" : "原始 I/O 记录")}\n" +
        $"时间：{Timestamp:yyyy-MM-dd HH:mm:ss.fffffff}\n方向：{Direction}    类型：{Record.Kind}\n" +
        $"端口 ID：{Record.PortId}    序号：{Record.Sequence}    请求 ID：{Record.RequestId}\n" +
        $"IOCTL：0x{Record.Ioctl:X8}    状态：0x{unchecked((uint)Record.Status):X8}\n" +
        $"请求长度：{Record.RequestedLength}    完成长度：{Record.TransferredLength}    偏移：{Record.Offset}\n" +
        $"标志：0x{Record.Flags:X4}（1=数据不可用，2=后续分片，4=跨会话完成）\n" +
        $"采集数据：{Record.Data.Length} 字节\n\nHEX\n{(RawHex.Length == 0 ? "（无已采集字节）" : RawHex)}\n\nASCII\n{_textContent}\n\n" +
        "控制指令是串口 I/O 请求，不是在线路上传输的数据帧；未采集的参数/返回值不作推断。";
    public string Result => Record.DataUnavailable ? "缓冲方式不支持/映射失败" : (Record.Flags & 4) != 0 ? "跨会话完成（保留原始记录）" : Record.Status < 0
        ? $"失败 0x{unchecked((uint)Record.Status):X8}" : $"完成 {Record.TransferredLength}/{Record.RequestedLength}";
    public ulong RequestId => Record.RequestId;
    public uint Offset => Record.Offset;

    [ObservableProperty]
    private string _content;

    public SerialMonitorRecordViewModel(SerialMonitorDataRecord record, bool isHexDisplay)
    {
        Record = record;
        Timestamp = record.Timestamp;
        Direction = record.Direction;
        _hexContent = HexHelper.BytesToHexString(record.Data);
        _textContent = HexHelper.BytesToAsciiString(record.Data, '.');
        if (record.Kind == 1)
        {
            (EventLabel, _controlContent) = SerialControlDecoder.Decode(record);
        }
        else EventLabel = record.IsFrame ? "定长帧" : "I/O 完成";
        _content = _controlContent ?? (isHexDisplay ? _hexContent : _textContent);
    }

    public bool Matches(bool showTx, bool showRx, string keyword)
    {
        if (Direction == SerialMonitorDataDirection.Tx && !showTx)
        {
            return false;
        }

        if (Direction == SerialMonitorDataDirection.Rx && !showRx)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(keyword)
            || EventLabel.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (_controlContent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Record.Kind == 1 && $"0x{Record.Ioctl:X8}".Contains(keyword, StringComparison.OrdinalIgnoreCase))
            || _hexContent.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || _textContent.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public void SetDisplayMode(bool isHexDisplay) =>
        Content = _controlContent ?? (isHexDisplay ? _hexContent : _textContent);

    public string ToCsvLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fffffff},{Direction.ToString().ToUpperInvariant()},{Escape(Content)},{Record.PortId},{Record.Sequence},{Record.RequestId},{EventLabel},0x{unchecked((uint)Record.Status):X8},{Record.RequestedLength},{Record.TransferredLength},{Record.Offset},{Record.Flags},0x{Record.Ioctl:X8},{RawHex}";

    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
}

public sealed partial class SerialMonitorViewModel : ObservableObject, IDisposable
{
    private readonly SerialMonitorService _monitorService;
    private readonly ISerialPortService _serialPortService;
    private readonly List<SerialMonitorRecordViewModel> _records = [];
    private readonly List<SerialMonitorDataRecord> _rawRecords = [];
    private MonitorFrameAssembler? _frameAssembler;
    private bool _disposed;

    public ObservableCollection<SerialPortInfo> Ports { get; } = [];
    public ObservableCollection<SerialMonitorRecordViewModel> FilteredRecords { get; } = [];
    public Action<Action>? DispatcherAction { get; set; }

    [ObservableProperty]
    private SerialPortInfo? _selectedPort;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _fixedFrameLength;

    [ObservableProperty]
    private string _captureStatus = "原始 I/O 完成记录；时间为请求完成时间，不代表线路逐字节时序。";

    [ObservableProperty]
    private bool _isHexDisplay = true;

    [ObservableProperty]
    private bool _showTx = true;

    [ObservableProperty]
    private bool _showRx = true;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _driverStatus = "未检测";

    [ObservableProperty]
    private string _permissionStatus = "未检测";

    [ObservableProperty]
    private string _operationStatus = "请选择串口后启动监控。";

    [ObservableProperty]
    private string _diagnosticStatus = "诊断：尚未读取。";

    public string StartStopButtonText => IsMonitoring ? "停止监控" : "启动监控";

    public SerialMonitorViewModel(SerialMonitorService monitorService, ISerialPortService serialPortService)
    {
        _monitorService = monitorService;
        _serialPortService = serialPortService;
        _monitorService.DataReceived += OnDataReceived;
        _monitorService.ErrorOccurred += OnErrorOccurred;
        _monitorService.StatusChanged += OnStatusChanged;
        RefreshPorts();
        CheckAvailability();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        Ports.Clear();
        foreach (var port in _serialPortService.GetAvailablePorts())
        {
            Ports.Add(port);
        }

        SelectedPort ??= Ports.FirstOrDefault();
    }

    [RelayCommand]
    private void CheckAvailability()
    {
        var availability = _monitorService.CheckAvailability();
        DriverStatus = availability.IsDriverAvailable ? "已加载" : "未加载";
        PermissionStatus = availability.IsAdministrator ? "管理员" : "非管理员";
        OperationStatus = availability.Message;
    }

    [RelayCommand]
    private void StartStopMonitoring()
    {
        if (IsMonitoring)
        {
            var stop = _monitorService.StopMonitoring();
            IsMonitoring = _monitorService.IsMonitoring;
            OperationStatus = stop.Message;
            return;
        }

        if (SelectedPort is null)
        {
            OperationStatus = "请选择目标串口。";
            return;
        }

        var start = _monitorService.StartMonitoring(SelectedPort.PortName);
        IsMonitoring = _monitorService.IsMonitoring;
        OperationStatus = start.Message;
        RefreshDiagnostics();
    }

    [RelayCommand]
    private void RefreshDiagnostics()
    {
        var diagnostic = _monitorService.GetDiagnostics();
        DiagnosticStatus = diagnostic is null
            ? "诊断：读取失败（驱动未响应或未加载）。"
            : $"诊断：{diagnostic.ToDisplayText()}";
    }

    [RelayCommand]
    private void ApplyPnpFilter()
    {
        if (SelectedPort is null)
        {
            OperationStatus = "请选择目标串口后应用 PnP 过滤器。";
            return;
        }

        var result = _monitorService.ApplyPnpFilter(SelectedPort.PortName);
        OperationStatus = result.Message;
    }

    [RelayCommand]
    private void ClearRecords()
    {
        _records.Clear();
        _rawRecords.Clear();
        _frameAssembler?.Reset();
        FilteredRecords.Clear();
    }

    public void AddRecord(SerialMonitorDataRecord record)
    {
        _rawRecords.Add(record);
        foreach (var item in _frameAssembler?.Append(record) ?? [record]) AddDisplayRecord(item);
    }

    private void AddDisplayRecord(SerialMonitorDataRecord record)
    {
        var item = new SerialMonitorRecordViewModel(record, IsHexDisplay);
        _records.Add(item);
        if (item.Matches(ShowTx, ShowRx, SearchKeyword))
        {
            FilteredRecords.Add(item);
        }
    }

    public void ExportRecords(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        File.WriteAllLines(filePath, ["Timestamp,Direction,Content,PortId,Sequence,RequestId,Event,Status,Requested,Transferred,Offset,Flags,Ioctl,RawHex", .. FilteredRecords.Select(x => x.ToCsvLine())]);
        OperationStatus = $"已导出 {FilteredRecords.Count} 条监控数据。";
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        OnPropertyChanged(nameof(StartStopButtonText));
        _frameAssembler?.Reset();
    }

    partial void OnFixedFrameLengthChanged(int value)
    {
        if (value is < 0 or > 65536)
        {
            FixedFrameLength = Math.Clamp(value, 0, 65536);
            return;
        }
        _frameAssembler = value == 0 ? null : new MonitorFrameAssembler(value);
        _records.Clear();
        FilteredRecords.Clear();
        foreach (var raw in _rawRecords)
            foreach (var record in _frameAssembler?.Append(raw) ?? [raw]) AddDisplayRecord(record);
    }
    partial void OnIsHexDisplayChanged(bool value)
    {
        foreach (var record in _records)
        {
            record.SetDisplayMode(value);
        }

        ApplyFilter();
    }

    partial void OnShowTxChanged(bool value) => ApplyFilter();
    partial void OnShowRxChanged(bool value) => ApplyFilter();
    partial void OnSearchKeywordChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredRecords.Clear();
        foreach (var record in _records.Where(x => x.Matches(ShowTx, ShowRx, SearchKeyword)))
        {
            FilteredRecords.Add(record);
        }
    }

    private void OnDataReceived(object? sender, SerialMonitorDataRecord record) =>
        Dispatch(() => AddRecord(record));

    private void OnErrorOccurred(object? sender, string message) =>
        Dispatch(() => { OperationStatus = message; IsMonitoring = _monitorService.IsMonitoring; });

    private void OnStatusChanged(object? sender, SerialMonitorStatus status) => Dispatch(() =>
        CaptureStatus = $"TX {status.TotalBytesTx} B / RX {status.TotalBytesRx} B；丢失 {status.DroppedRecords} 条 / {status.DroppedBytes} B。时间为 I/O 完成时间。" );

    private void Dispatch(Action action)
    {
        if (DispatcherAction is null)
        {
            action();
        }
        else
        {
            DispatcherAction(action);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _monitorService.DataReceived -= OnDataReceived;
        _monitorService.ErrorOccurred -= OnErrorOccurred;
        _monitorService.StatusChanged -= OnStatusChanged;
        _monitorService.StopMonitoring();
        _disposed = true;
    }
}
