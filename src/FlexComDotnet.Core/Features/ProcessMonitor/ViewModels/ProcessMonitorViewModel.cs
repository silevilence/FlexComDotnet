using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Services;
using FlexComDotnet.Core.Features.Capture.Services;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Capture.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexComDotnet.Core.Features.ProcessMonitor.ViewModels;

public partial class ProcessMonitorViewModel : ObservableObject
{
    private readonly IConfigurationService _configuration;
    private readonly ProcessMonitorSession _session;
    private readonly ProcessMonitorTargets _targets;
    private readonly CaptureProcessing _processing;
    private readonly IProtocolParserService _parsers;
    private CaptureFrameAssembler? _assembler;
    private long _processedThrough;
    private Guid _frameSession;
    private int _activeFrameLength;
    private readonly Queue<CaptureFrame> _frameHistory = new();
    private string? _lastDisplayOptions;
    private long _lastDisplayCount = -1;
    private Guid _lastDisplaySession;
    private long _evictedFrames;
    private IProtocolParser? _lastParser;
    private string? _lastParserName;
    private Dictionary<CaptureFrame, ProcessMonitorFrameRow> _parsedFrames = [];
    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AttachCommand))] private ProcessMonitorTarget? _selectedTarget;
    [ObservableProperty] private IReadOnlyList<ProcessMonitorRow> _records = [];
    [ObservableProperty] private IReadOnlyList<ProcessMonitorFrameRow> _frames = [];
    [ObservableProperty] private bool _framingEnabled;
    [ObservableProperty] private int _frameLength = 7;
    [ObservableProperty] private string _parserName = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showHex = true;
    [ObservableProperty] private CaptureDirection? _directionFilter;
    [ObservableProperty] private string _analysisStatus = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(StartCommand))] private string _executablePath = string.Empty;
    [ObservableProperty] private string _arguments = string.Empty;
    [ObservableProperty] private string _portFilter = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AttachCommand), nameof(StartCommand), nameof(StopCommand))] private bool _isMonitoring;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AttachCommand), nameof(StartCommand), nameof(StopCommand))] private bool _isBusy;
    [ObservableProperty] private string _operationStatus = "选择进程后附加，或指定程序路径启动。";
    [ObservableProperty] private string _sessionStatus = "未开始";
    [ObservableProperty] private string _statistics = string.Empty;
    [ObservableProperty] private string _processFilter = string.Empty;
    [ObservableProperty] private IReadOnlyList<ProcessMonitorTarget> _processes = [];
    [ObservableProperty] private IReadOnlyList<string> _parserNames = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RecordDetails))] private ProcessMonitorRow? _selectedRecord;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FrameDetails))] private ProcessMonitorFrameRow? _selectedFrame;
    public string RecordDetails => SelectedRecord is null ? string.Empty : JsonSerializer.Serialize(SelectedRecord.Record, s_json);
    public string FrameDetails => SelectedFrame is null ? string.Empty : JsonSerializer.Serialize(SelectedFrame, s_json);
    public static IReadOnlyList<KeyValuePair<string, CaptureDirection?>> Directions { get; } =
        [new("全部方向", null), new("发送", CaptureDirection.Transmit), new("接收", CaptureDirection.Receive), new("控制", CaptureDirection.Control)];

    public ProcessMonitorViewModel(IConfigurationService configuration, ProcessMonitorSession session,
        ProcessMonitorTargets targets, IProtocolParserService parsers)
    {
        _configuration = configuration;
        _session = session;
        _targets = targets;
        _processing = new(parsers);
        _parsers = parsers;
        var config = configuration.Load().ProcessMonitor ?? new();
        ExecutablePath = config.ExecutablePath ?? string.Empty;
        Arguments = config.Arguments ?? string.Empty;
        PortFilter = config.PortFilter ?? string.Empty;
        SearchText = config.SearchText ?? string.Empty;
        ShowHex = config.ShowHex;
        DirectionFilter = config.DirectionFilter is { } direction && Enum.IsDefined(direction) ? direction : null;
        FramingEnabled = config.FramingEnabled;
        FrameLength = config.FrameLength is >= 1 and <= 65536 ? config.FrameLength : 7;
        ParserName = config.ParserName ?? string.Empty;
    }

    private bool CanAttach() => !IsBusy && !IsMonitoring && SelectedTarget is { Architecture: "x86" or "x64" }
        && SelectedTarget.ProcessId != Environment.ProcessId;
    private bool CanStart() => !IsBusy && !IsMonitoring && !string.IsNullOrWhiteSpace(ExecutablePath);
    private bool CanStop() => !IsBusy && IsMonitoring;

    [RelayCommand]
    private async Task RefreshTargetsAsync()
    {
        try
        {
            var targets = await Task.Run(_targets.Enumerate);
            Processes = targets.Where(t => string.IsNullOrWhiteSpace(ProcessFilter)
                || t.Name.Contains(ProcessFilter, StringComparison.OrdinalIgnoreCase)
                || t.ProcessId.ToString().Contains(ProcessFilter, StringComparison.Ordinal)).ToArray();
            OperationStatus = $"找到 {Processes.Count} 个可访问进程。权限不足或已退出的进程不在列表中；不会自动提权。";
        }
        catch (Exception error) { OperationStatus = error.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanAttach), IncludeCancelCommand = true)]
    private async Task AttachAsync(CancellationToken cancellationToken)
    {
        if (!CanAttach()) return;
        IsBusy = true;
        try { await _session.AttachAsync(SelectedTarget!, cancellationToken); OperationStatus = _session.Status; }
        catch (OperationCanceledException) { OperationStatus = "附加已取消。"; }
        catch (Exception error) { OperationStatus = error.Message; }
        finally { IsBusy = false; Refresh(); }
    }

    [RelayCommand(CanExecute = nameof(CanStart), IncludeCancelCommand = true)]
    private async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!CanStart()) return;
        IsBusy = true;
        try
        {
            await using var launch = await _targets.PrepareLaunchAsync(ExecutablePath, Arguments, cancellationToken);
            await _session.StartAsync(launch, cancellationToken);
            SelectedTarget = launch.Target;
            OperationStatus = _session.Status;
        }
        catch (OperationCanceledException) { OperationStatus = "启动已取消。"; }
        catch (Exception error) { OperationStatus = error.Message; }
        finally { IsBusy = false; Refresh(); }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        if (!CanStop()) return;
        IsBusy = true;
        try { await _session.StopAsync(); OperationStatus = _session.Status; }
        catch (Exception error) { OperationStatus = error.Message; }
        finally { IsBusy = false; Refresh(); }
    }

    /// <summary>Called on the presentation thread; capture never waits for UI dispatch.</summary>
    public void Refresh()
    {
        IsMonitoring = _session.IsMonitoring;
        SessionStatus = _session.Status;
        var parserNames = _parsers.GetAllParsers().Select(p => p.Name).Prepend(string.Empty).ToArray();
        if (!ParserNames.SequenceEqual(parserNames)) ParserNames = parserNames;
        var state = _session.Snapshot();
        var snapshot = state.Records;
        var total = state.EvictedRecords + snapshot.Count;
        var parser = _parsers.GetParser(ParserName);
        var parserChanged = !ReferenceEquals(parser, _lastParser) || ParserName != _lastParserName;
        var options = JsonSerializer.Serialize(new { PortFilter, DirectionFilter, SearchText, ShowHex, FramingEnabled, FrameLength, ParserName });
        if (_lastDisplaySession == state.SessionId && _lastDisplayCount == total && _lastDisplayOptions == options && !parserChanged) return;
        if (parserChanged) _parsedFrames.Clear();
        _lastParser = parser;
        _lastParserName = ParserName;
        _lastDisplaySession = state.SessionId; _lastDisplayCount = total; _lastDisplayOptions = options;
        Statistics = $"保留 {snapshot.Count} 条 · 已淘汰 {state.EvictedRecords} 条 · 采集丢弃 {snapshot.LastOrDefault()?.DroppedRecords ?? 0} 条";
        var selected = SelectedRecord?.Record;
        Records = snapshot.Where(Matches).Select(r => new ProcessMonitorRow(r, _processing.Format(r.ToProcessingRecord(), ShowHex))).ToArray();
        SelectedRecord = Records.FirstOrDefault(r => r.Record == selected);
        Frames = [];
        AnalysisStatus = string.Empty;
        if (!FramingEnabled) { _assembler = null; _frameHistory.Clear(); _parsedFrames.Clear(); return; }
        if (FrameLength is < 1 or > 65536) { AnalysisStatus = "固定帧长应为 1～65536 字节。"; return; }
        if (_assembler is null || _frameSession != state.SessionId || _activeFrameLength != FrameLength)
        {
            _assembler = new(FrameLength); _frameSession = state.SessionId; _activeFrameLength = FrameLength;
            _frameHistory.Clear(); _processedThrough = state.EvictedRecords; _evictedFrames = 0;
        }
        if (_processedThrough < state.EvictedRecords)
        {
            _assembler.Reset(); _processedThrough = state.EvictedRecords;
            AnalysisStatus = "页面未处理的原始记录已淘汰，已断开部分帧。";
        }
        // All records, including gaps and hidden controls, reach framing before display filters.
        foreach (var record in snapshot.Skip((int)(_processedThrough - state.EvictedRecords)))
        foreach (var frame in _assembler.Append(record.ToProcessingRecord()))
        {
            if (_frameHistory.Count == 2000) { _frameHistory.Dequeue(); _evictedFrames++; }
            _frameHistory.Enqueue(frame);
        }
        _processedThrough = total;
        var frames = new List<ProcessMonitorFrameRow>();
        var cache = new Dictionary<CaptureFrame, ProcessMonitorFrameRow>();
        foreach (var frame in _frameHistory)
        {
            if (_parsedFrames.TryGetValue(frame, out var existing)) { frames.Add(existing); cache.Add(frame, existing); continue; }
            ParsedFrame? parsed = null;
            if (!string.IsNullOrWhiteSpace(ParserName))
            {
                try { parsed = _processing.Parse(frame, ParserName); }
                catch (Exception error) { parsed = new() { IsValid = false, ErrorMessage = error.Message, RawData = frame.Data }; }
            }
            var row = new ProcessMonitorFrameRow(frame, string.Join(" ", frame.Data.Select(b => b.ToString("X2"))), parsed);
            frames.Add(row); cache.Add(frame, row);
        }
        _parsedFrames = cache;
        var selectedFrame = SelectedFrame?.Frame;
        Frames = frames;
        SelectedFrame = Frames.FirstOrDefault(f => f.Frame == selectedFrame);
        if (_evictedFrames > 0) AnalysisStatus += $" 仅保留最近 2000 帧，已淘汰 {_evictedFrames} 帧。";
        if (_assembler.DiscardedPartialStreams > 0) AnalysisStatus += $" 部分流容量已满，已丢弃 {_assembler.DiscardedPartialStreams} 个部分帧。";
    }

    private bool Matches(ProcessMonitorRecord record) =>
        (string.IsNullOrWhiteSpace(PortFilter) || string.Equals(record.PortName, PortFilter.Trim(), StringComparison.OrdinalIgnoreCase))
        && (DirectionFilter is null || record.Direction == DirectionFilter)
        && (_processing.Matches(record.ToProcessingRecord(), SearchText)
            || record.Api.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || record.PortName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || record.Detail.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public string ExportVisibleJson() => JsonSerializer.Serialize(Records.Select(r => r.Record), s_json);
    public string ExportFramesJson() => JsonSerializer.Serialize(Frames, s_json);

    [RelayCommand]
    private void SaveConfiguration()
    {
        var config = _configuration.Load();
        config.ProcessMonitor ??= new();
        config.ProcessMonitor.ExecutablePath = ExecutablePath;
        config.ProcessMonitor.Arguments = Arguments;
        config.ProcessMonitor.PortFilter = PortFilter;
        config.ProcessMonitor.SearchText = SearchText;
        config.ProcessMonitor.ShowHex = ShowHex;
        config.ProcessMonitor.DirectionFilter = DirectionFilter;
        config.ProcessMonitor.FramingEnabled = FramingEnabled;
        config.ProcessMonitor.FrameLength = FrameLength;
        config.ProcessMonitor.ParserName = ParserName;
        OperationStatus = _configuration.Save(config) ? "进程监控偏好已保存。" : "保存失败，请检查配置目录权限。";
    }
}

public sealed record ProcessMonitorRow(ProcessMonitorRecord Record, string DataText);
public sealed record ProcessMonitorFrameRow(CaptureFrame Frame, string DataText, ParsedFrame? Parsed);
