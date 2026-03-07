using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;

namespace FlexComDotnet.Core.Features.Logging.ViewModels;

/// <summary>
/// 来源筛选项（支持多选）
/// </summary>
public partial class SourceFilterItem : ObservableObject
{
    private readonly Action _onChanged;

    public LogSource Source { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    partial void OnIsSelectedChanged(bool value) => _onChanged();

    public SourceFilterItem(LogSource source, string displayName, Action onChanged)
    {
        Source = source;
        DisplayName = displayName;
        _onChanged = onChanged;
    }
}

/// <summary>
/// 日志面板 ViewModel - 提供筛选、显示功能
/// </summary>
public partial class LogPanelViewModel : ObservableObject, IDisposable
{
    private readonly ILoggingService _loggingService;
    private readonly List<LogEntry> _allEntries = [];
    private bool _disposed;
    private Regex? _compiledRegex;

    /// <summary>
    /// 筛选后的日志条目
    /// </summary>
    public ObservableCollection<LogEntry> FilteredEntries { get; } = [];

    /// <summary>
    /// 来源筛选项集合（多选）
    /// </summary>
    public ObservableCollection<SourceFilterItem> SourceFilters { get; } = [];

    /// <summary>
    /// UI 线程调度器 - 由 View 层注入
    /// </summary>
    public Action<Action>? DispatcherAction { get; set; }

    #region 筛选属性

    [ObservableProperty]
    private bool _showDebug = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private bool _showWarning = true;

    [ObservableProperty]
    private bool _showError = true;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    /// <summary>
    /// 是否启用正则表达式搜索
    /// </summary>
    [ObservableProperty]
    private bool _useRegex;

    /// <summary>
    /// 正则表达式是否有语法错误
    /// </summary>
    [ObservableProperty]
    private bool _isRegexError;

    #endregion

    public LogPanelViewModel(ILoggingService loggingService)
    {
        _loggingService = loggingService;

        // 初始化来源筛选项
        InitSourceFilters();

        // 加载已有日志
        foreach (var entry in _loggingService.Entries)
        {
            _allEntries.Add(entry);
        }

        // 订阅新日志事件
        _loggingService.LogAdded += OnLogAdded;

        // 初始化筛选结果
        ApplyFilter();
    }

    private void InitSourceFilters()
    {
        foreach (LogSource source in Enum.GetValues<LogSource>())
        {
            var displayName = GetSourceDisplayName(source);
            SourceFilters.Add(new SourceFilterItem(source, displayName, ApplyFilter));
        }
    }

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

    #region 属性变更处理

    partial void OnShowDebugChanged(bool value) => ApplyFilter();
    partial void OnShowInfoChanged(bool value) => ApplyFilter();
    partial void OnShowWarningChanged(bool value) => ApplyFilter();
    partial void OnShowErrorChanged(bool value) => ApplyFilter();

    partial void OnSearchKeywordChanged(string value)
    {
        UpdateCompiledRegex();
        ApplyFilter();
    }

    partial void OnUseRegexChanged(bool value)
    {
        UpdateCompiledRegex();
        ApplyFilter();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ClearLogs()
    {
        _allEntries.Clear();
        FilteredEntries.Clear();
    }

    [RelayCommand]
    private void SelectAllSources()
    {
        foreach (var sf in SourceFilters)
            sf.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoneSources()
    {
        foreach (var sf in SourceFilters)
            sf.IsSelected = false;
    }

    [RelayCommand]
    private void InvertSources()
    {
        foreach (var sf in SourceFilters)
            sf.IsSelected = !sf.IsSelected;
    }

    #endregion

    #region 事件处理

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        void AddEntry()
        {
            _allEntries.Add(entry);
            if (MatchesFilter(entry))
            {
                FilteredEntries.Add(entry);
            }
        }

        if (DispatcherAction != null)
        {
            DispatcherAction(AddEntry);
        }
        else
        {
            AddEntry();
        }
    }

    #endregion

    #region 筛选逻辑

    private void ApplyFilter()
    {
        FilteredEntries.Clear();

        foreach (var entry in _allEntries)
        {
            if (MatchesFilter(entry))
            {
                FilteredEntries.Add(entry);
            }
        }
    }

    private bool MatchesFilter(LogEntry entry)
    {
        // 等级筛选
        var levelMatch = entry.Level switch
        {
            LogLevel.Debug => ShowDebug,
            LogLevel.Info => ShowInfo,
            LogLevel.Warning => ShowWarning,
            LogLevel.Error => ShowError,
            _ => true
        };
        if (!levelMatch) return false;

        // 来源多选筛选
        var sourceFilter = SourceFilters.FirstOrDefault(sf => sf.Source == entry.Source);
        if (sourceFilter != null && !sourceFilter.IsSelected)
            return false;

        // 关键词/正则搜索
        if (!string.IsNullOrEmpty(SearchKeyword))
        {
            if (UseRegex)
            {
                if (_compiledRegex == null) return false; // 无效正则不匹配
                if (!_compiledRegex.IsMatch(entry.Message)) return false;
            }
            else
            {
                if (!entry.Message.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    private void UpdateCompiledRegex()
    {
        IsRegexError = false;
        _compiledRegex = null;

        if (UseRegex && !string.IsNullOrEmpty(SearchKeyword))
        {
            try
            {
                _compiledRegex = new Regex(SearchKeyword, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
            }
            catch (RegexParseException)
            {
                IsRegexError = true;
            }
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _loggingService.LogAdded -= OnLogAdded;
        GC.SuppressFinalize(this);
    }
}
