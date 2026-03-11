using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;
using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Scripting.ViewModels;

/// <summary>
/// 脚本系统 ViewModel
/// </summary>
public partial class ScriptingViewModel : ObservableObject, IDisposable
{
    private readonly IScriptEngine _engine;
    private readonly IScriptManager _manager;
    private readonly IScriptApiBridge _bridge;
    private readonly IScriptHookService? _hookService;
    private readonly ILoggingService? _loggingService;
    private readonly IConfigurationService? _configurationService;
    private bool _disposed;
    private bool _isLoadingHooks;

    #region Observable Properties

    /// <summary>
    /// 脚本列表
    /// </summary>
    public ObservableCollection<ScriptFileInfo> Scripts { get; } = [];

    /// <summary>
    /// 当前选中的脚本
    /// </summary>
    [ObservableProperty]
    private ScriptFileInfo? _selectedScript;

    /// <summary>
    /// 编辑器内容
    /// </summary>
    [ObservableProperty]
    private string _editorContent = string.Empty;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunScriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScriptCommand))]
    private bool _isRunning;

    /// <summary>
    /// 状态文本
    /// </summary>
    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>
    /// 新建脚本名称
    /// </summary>
    [ObservableProperty]
    private string _newScriptName = string.Empty;

    /// <summary>
    /// 日志条目列表
    /// </summary>
    public ObservableCollection<ScriptLogEntry> LogEntries { get; } = [];

    /// <summary>
    /// UI 线程调度器 - 由 View 层注入。
    /// 在 UI 线程调用时直接执行，在后台线程调用时同步分发到 UI 线程。
    /// </summary>
    public Action<Action>? DispatcherAction { get; set; }

    /// <summary>
    /// 请求打开编辑器窗口事件 (由 View 层订阅)
    /// </summary>
    public event EventHandler? OpenEditorRequested;

    /// <summary>
    /// 确认删除回调（由 View 层注入，参数为提示消息，返回是否确认）
    /// </summary>
    public Func<string, bool>? ConfirmAction { get; set; }

    #region Hook 相关属性

    /// <summary>
    /// 是否支持 Hook 功能
    /// </summary>
    public bool IsHookSupported => _hookService != null;

    /// <summary>
    /// 接收预处理 Hook 启用状态
    /// </summary>
    [ObservableProperty]
    private bool _rxHookEnabled;

    /// <summary>
    /// 接收预处理 Hook 脚本 ID
    /// </summary>
    [ObservableProperty]
    private string? _rxHookScriptId;

    /// <summary>
    /// 发送后处理 Hook 启用状态
    /// </summary>
    [ObservableProperty]
    private bool _txHookEnabled;

    /// <summary>
    /// 发送后处理 Hook 脚本 ID
    /// </summary>
    [ObservableProperty]
    private string? _txHookScriptId;

    /// <summary>
    /// 应答 Hook 启用状态
    /// </summary>
    [ObservableProperty]
    private bool _replyHookEnabled;

    /// <summary>
    /// 应答 Hook 脚本 ID
    /// </summary>
    [ObservableProperty]
    private string? _replyHookScriptId;

    #endregion

    #endregion

    public ScriptingViewModel(
        IScriptEngine engine,
        IScriptManager manager,
        IScriptApiBridge bridge,
        IScriptHookService? hookService = null,
        ILoggingService? loggingService = null,
        IConfigurationService? configurationService = null)
    {
        _engine = engine;
        _manager = manager;
        _bridge = bridge;
        _hookService = hookService;
        _loggingService = loggingService;
        _configurationService = configurationService;

        // 订阅引擎事件
        _engine.StateChanged += OnEngineStateChanged;
        _engine.LogOutput += OnEngineLogOutput;
        _engine.ErrorOccurred += OnEngineErrorOccurred;

        // 订阅管理器事件
        _manager.ScriptsChanged += OnScriptsChanged;

        // 订阅 Hook 服务日志
        if (_hookService != null)
        {
            _hookService.LogOutput += OnHookLogOutput;
            LoadHookSettings();
        }

        // 加载脚本列表
        RefreshScriptList();
    }

    #region 属性变更处理

    partial void OnSelectedScriptChanged(ScriptFileInfo? value)
    {
        if (value != null)
        {
            var content = _manager.ReadScriptContent(value.Id);
            EditorContent = content ?? string.Empty;
        }
        else
        {
            EditorContent = string.Empty;
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// 创建新脚本
    /// </summary>
    [RelayCommand]
    private void CreateNewScript()
    {
        if (string.IsNullOrWhiteSpace(NewScriptName))
            return;

        var script = _manager.CreateScript(NewScriptName);
        NewScriptName = string.Empty;
        RefreshScriptList();
        SelectedScript = Scripts.FirstOrDefault(s => s.Id == script.Id);
    }

    /// <summary>
    /// 删除选中的脚本
    /// </summary>
    [RelayCommand]
    private void DeleteScript()
    {
        if (SelectedScript == null) return;

        var scriptId = SelectedScript.Id;
        var scriptName = SelectedScript.Name;

        // 检查是否被 Hook 引用
        var boundHooks = GetBoundHookNames(scriptId);
        if (boundHooks.Count > 0)
        {
            var hookNames = string.Join("、", boundHooks);
            var message = $"脚本 \"{scriptName}\" 正被以下 Hook 引用：{hookNames}。\n删除后将自动解除绑定，是否继续？";

            if (ConfirmAction != null && !ConfirmAction(message))
            {
                return;
            }

            // 级联清理 Hook 绑定
            ClearHookBindingsForScript(scriptId);
        }

        _manager.DeleteScript(scriptId);
        SelectedScript = null;
        RefreshScriptList();
    }

    /// <summary>
    /// 获取引用指定脚本的 Hook 名称列表
    /// </summary>
    private List<string> GetBoundHookNames(string scriptId)
    {
        var hooks = new List<string>();
        if (RxHookScriptId == scriptId) hooks.Add("Rx预处理");
        if (TxHookScriptId == scriptId) hooks.Add("Tx后处理");
        if (ReplyHookScriptId == scriptId) hooks.Add("Reply应答");
        return hooks;
    }

    /// <summary>
    /// 清除引用指定脚本的所有 Hook 绑定
    /// </summary>
    private void ClearHookBindingsForScript(string scriptId)
    {
        if (RxHookScriptId == scriptId)
        {
            RxHookEnabled = false;
            RxHookScriptId = null;
        }
        if (TxHookScriptId == scriptId)
        {
            TxHookEnabled = false;
            TxHookScriptId = null;
        }
        if (ReplyHookScriptId == scriptId)
        {
            ReplyHookEnabled = false;
            ReplyHookScriptId = null;
        }
    }

    /// <summary>
    /// 运行脚本
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunScript))]
    private async Task RunScript()
    {
        if (SelectedScript == null) return;

        var content = EditorContent;
        var scriptName = SelectedScript.Name;

        var result = await _engine.ExecuteAsync(content, scriptName);

        if (!result.Success)
        {
            StatusText = $"执行失败: {result.ErrorMessage}";
        }
        else
        {
            StatusText = $"执行完成 ({result.ElapsedMs}ms)";
        }
    }

    private bool CanRunScript() => !IsRunning;

    /// <summary>
    /// 停止脚本
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopScript))]
    private void StopScript()
    {
        _engine.Stop();
    }

    private bool CanStopScript() => IsRunning;

    /// <summary>
    /// 保存脚本
    /// </summary>
    [RelayCommand]
    private void SaveScript()
    {
        if (SelectedScript == null) return;

        _manager.SaveScriptContent(SelectedScript.Id, EditorContent);
        StatusText = "已保存";
    }

    /// <summary>
    /// 清空日志
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        LogEntries.Clear();
    }

    /// <summary>
    /// 打开脚本编辑器窗口
    /// </summary>
    [RelayCommand]
    private void OpenEditor()
    {
        OpenEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 重命名当前选中的脚本
    /// </summary>
    public void RenameScript(string newName)
    {
        if (SelectedScript == null || string.IsNullOrWhiteSpace(newName)) return;

        if (_manager.UpdateScriptInfo(SelectedScript.Id, newName))
        {
            RefreshScriptList();
            // 重新选中该脚本
            SelectedScript = Scripts.FirstOrDefault(s => s.Name == newName);
            StatusText = "已重命名";
        }
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 安全地在 UI 线程上执行操作。
    /// 使用 View 层注入的 Dispatcher（WPF Dispatcher.Invoke 自动判断：
    /// 若在 UI 线程则直接执行，否则同步切换到 UI 线程），
    /// 未注入时直接执行（适用于单元测试场景）。
    /// </summary>
    private void RunOnUiThread(Action action)
    {
        if (DispatcherAction != null)
        {
            DispatcherAction(action);
        }
        else
        {
            action();
        }
    }

    private void OnEngineStateChanged(object? sender, ScriptState state)
    {
        RunOnUiThread(() =>
        {
            IsRunning = state == ScriptState.Running;
            StatusText = state switch
            {
                ScriptState.Idle => "就绪",
                ScriptState.Running => "运行中...",
                ScriptState.Error => "错误",
                ScriptState.Stopping => "停止中...",
                _ => "就绪"
            };
        });
    }

    private void OnEngineLogOutput(object? sender, ScriptLogEntry entry)
    {
        RunOnUiThread(() =>
        {
            LogEntries.Add(entry);
        });

        // 转发到统一日志服务
        var level = entry.Level switch
        {
            ScriptLogLevel.Debug => Logging.Models.LogLevel.Debug,
            ScriptLogLevel.Info => Logging.Models.LogLevel.Info,
            ScriptLogLevel.Warning => Logging.Models.LogLevel.Warning,
            ScriptLogLevel.Error => Logging.Models.LogLevel.Error,
            _ => Logging.Models.LogLevel.Info
        };
        _loggingService?.Log(level, LogSource.Script, entry.Message);
    }

    private void OnEngineErrorOccurred(object? sender, string errorMessage)
    {
        RunOnUiThread(() =>
        {
            LogEntries.Add(new ScriptLogEntry
            {
                Level = ScriptLogLevel.Error,
                Message = errorMessage,
                ScriptName = _engine.CurrentScriptName ?? "unknown"
            });
        });

        _loggingService?.Error(LogSource.Script, errorMessage);
    }

    private void OnScriptsChanged(object? sender, EventArgs args)
    {
        RefreshScriptList();
    }

    private void OnHookLogOutput(object? sender, ScriptLogEntry entry)
    {
        RunOnUiThread(() =>
        {
            LogEntries.Add(entry);
        });

        // 转发到统一日志服务
        var level = entry.Level switch
        {
            ScriptLogLevel.Debug => Logging.Models.LogLevel.Debug,
            ScriptLogLevel.Info => Logging.Models.LogLevel.Info,
            ScriptLogLevel.Warning => Logging.Models.LogLevel.Warning,
            ScriptLogLevel.Error => Logging.Models.LogLevel.Error,
            _ => Logging.Models.LogLevel.Info
        };
        _loggingService?.Log(level, LogSource.Script, entry.Message);
    }

    #endregion

    #region Hook 属性变更处理

    partial void OnRxHookEnabledChanged(bool value)
    {
        _hookService?.SetHookEnabled(HookType.RxPreProcessor, value);
    }

    partial void OnRxHookScriptIdChanged(string? value)
    {
        _hookService?.SetHookScript(HookType.RxPreProcessor, value);
        SaveHookConfig();
    }

    partial void OnTxHookEnabledChanged(bool value)
    {
        _hookService?.SetHookEnabled(HookType.TxPostProcessor, value);
    }

    partial void OnTxHookScriptIdChanged(string? value)
    {
        _hookService?.SetHookScript(HookType.TxPostProcessor, value);
        SaveHookConfig();
    }

    partial void OnReplyHookEnabledChanged(bool value)
    {
        _hookService?.SetHookEnabled(HookType.Reply, value);
    }

    partial void OnReplyHookScriptIdChanged(string? value)
    {
        _hookService?.SetHookScript(HookType.Reply, value);
        SaveHookConfig();
    }

    #endregion

    #region 私有方法

    private void RefreshScriptList()
    {
        var scripts = _manager.GetAllScripts();
        Scripts.Clear();
        foreach (var script in scripts)
        {
            Scripts.Add(script);
        }
    }

    private void LoadHookSettings()
    {
        if (_hookService == null) return;

        _isLoadingHooks = true;
        try
        {
            // 从持久化配置恢复脚本绑定
            if (_configurationService != null)
            {
                var appConfig = _configurationService.Load();
                var hookConfig = appConfig.ScriptHookConfig;

                // 恢复脚本绑定到服务（不恢复启用状态）
                _hookService.SetHookScript(HookType.RxPreProcessor, hookConfig.RxPreProcessorScriptId);
                _hookService.SetHookScript(HookType.TxPostProcessor, hookConfig.TxPostProcessorScriptId);
                _hookService.SetHookScript(HookType.Reply, hookConfig.ReplyScriptId);
            }

            // 从服务读取最新状态到 ViewModel
            var settings = _hookService.Settings;
            RxHookEnabled = settings.RxPreProcessor.IsEnabled;
            RxHookScriptId = settings.RxPreProcessor.ScriptId;
            TxHookEnabled = settings.TxPostProcessor.IsEnabled;
            TxHookScriptId = settings.TxPostProcessor.ScriptId;
            ReplyHookEnabled = settings.Reply.IsEnabled;
            ReplyHookScriptId = settings.Reply.ScriptId;
        }
        finally
        {
            _isLoadingHooks = false;
        }
    }

    /// <summary>
    /// 保存 Hook 脚本绑定到配置文件（不保存启用状态）
    /// </summary>
    private void SaveHookConfig()
    {
        if (_configurationService == null || _isLoadingHooks) return;

        var appConfig = _configurationService.Load();
        appConfig.ScriptHookConfig = new ScriptHookConfig
        {
            RxPreProcessorScriptId = RxHookScriptId,
            TxPostProcessorScriptId = TxHookScriptId,
            ReplyScriptId = ReplyHookScriptId
        };
        _configurationService.Save(appConfig);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _engine.StateChanged -= OnEngineStateChanged;
        _engine.LogOutput -= OnEngineLogOutput;
        _engine.ErrorOccurred -= OnEngineErrorOccurred;
        _manager.ScriptsChanged -= OnScriptsChanged;

        if (_hookService != null)
        {
            _hookService.LogOutput -= OnHookLogOutput;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
