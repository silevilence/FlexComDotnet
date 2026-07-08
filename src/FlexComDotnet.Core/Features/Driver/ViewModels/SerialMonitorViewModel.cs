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

    public DateTime Timestamp { get; }
    public SerialMonitorDataDirection Direction { get; }

    [ObservableProperty]
    private string _content;

    public SerialMonitorRecordViewModel(SerialMonitorDataRecord record, bool isHexDisplay)
    {
        Timestamp = record.Timestamp;
        Direction = record.Direction;
        _hexContent = HexHelper.BytesToHexString(record.Data);
        _textContent = HexHelper.BytesToAsciiString(record.Data, '.');
        _content = isHexDisplay ? _hexContent : _textContent;
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
            || _hexContent.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || _textContent.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public void SetDisplayMode(bool isHexDisplay) =>
        Content = isHexDisplay ? _hexContent : _textContent;

    public string ToCsvLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff},{Direction.ToString().ToUpperInvariant()},{Content}";
}

public sealed partial class SerialMonitorViewModel : ObservableObject, IDisposable
{
    private readonly SerialMonitorService _monitorService;
    private readonly ISerialPortService _serialPortService;
    private readonly List<SerialMonitorRecordViewModel> _records = [];
    private bool _disposed;

    public ObservableCollection<SerialPortInfo> Ports { get; } = [];
    public ObservableCollection<SerialMonitorRecordViewModel> FilteredRecords { get; } = [];
    public Action<Action>? DispatcherAction { get; set; }

    [ObservableProperty]
    private SerialPortInfo? _selectedPort;

    [ObservableProperty]
    private bool _isMonitoring;

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

    public string StartStopButtonText => IsMonitoring ? "停止监控" : "启动监控";

    public SerialMonitorViewModel(SerialMonitorService monitorService, ISerialPortService serialPortService)
    {
        _monitorService = monitorService;
        _serialPortService = serialPortService;
        _monitorService.DataReceived += OnDataReceived;
        _monitorService.ErrorOccurred += OnErrorOccurred;
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
        CheckAvailability();
    }

    [RelayCommand]
    private void ClearRecords()
    {
        _records.Clear();
        FilteredRecords.Clear();
    }

    public void AddRecord(SerialMonitorDataRecord record)
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

        File.WriteAllLines(filePath, ["Timestamp,Direction,Content", .. FilteredRecords.Select(x => x.ToCsvLine())]);
        OperationStatus = $"已导出 {FilteredRecords.Count} 条监控数据。";
    }

    partial void OnIsMonitoringChanged(bool value) => OnPropertyChanged(nameof(StartStopButtonText));
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
        Dispatch(() => OperationStatus = message);

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
        _monitorService.StopMonitoring();
        _disposed = true;
    }
}
