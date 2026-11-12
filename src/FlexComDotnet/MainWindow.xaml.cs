using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;
using FlexComDotnet.Core.Features.Checksum.ViewModels;
using FlexComDotnet.Core.Features.AutoReply.ViewModels;
using FlexComDotnet.Core.Features.Network.ViewModels;
using FlexComDotnet.Core.Features.Network.Services;
using FlexComDotnet.Core.Features.Update.ViewModels;
using FlexComDotnet.Core.Features.Settings.ViewModels;
using FlexComDotnet.Features.Serial.Views;
using FlexComDotnet.Features.Network.Views;
using FlexComDotnet.Features.Checksum.Views;
using FlexComDotnet.Features.AutoReply.Views;
using FlexComDotnet.Features.Update.Views;
using FlexComDotnet.Features.Settings.Views;
using FlexComDotnet.Features.Scripting.Views;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using FlexComDotnet.Core.Features.Protocol.ViewModels;
using FlexComDotnet.Features.Protocol.Views;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Visualization.ViewModels;
using FlexComDotnet.Core.Features.Visualization.Services;
using FlexComDotnet.Features.Visualization.Views;
using FlexComDotnet.Core.Features.Logging.ViewModels;
using FlexComDotnet.Core.Features.Logging.Services;
using FlexComDotnet.Features.Logging.Views;
using FlexComDotnet.Core.Features.Driver.ViewModels;
using FlexComDotnet.Features.Driver.Views;
namespace FlexComDotnet;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Features.Layout.Services.DockLayoutStore _layoutStore;
    private bool _closeApproved;
    private bool _closeInProgress;
    private readonly IPanelManager _panelManager;
    private readonly IConfigurationService _configService;
    private readonly ConnectionConfigView _connectionConfigView;
    private readonly CommandListView _commandListView;
    private readonly AutoReplyView _autoReplyView;
    private readonly ScriptingView _scriptingView;
    private readonly DataVisualizationView _dataVisualizationView;
    private readonly LogPanelView _logPanelView;
    private readonly SerialMonitorView _serialMonitorView;
    private readonly SerialCommunicationView _serialCommunicationView;

    /// <summary>
    /// 面板 ID 常量
    /// </summary>
    private static class PanelIds
    {
        public const string ConnectionConfig = "connection-config";
        public const string CommandList = "command-list";
        public const string AutoReply = "auto-reply";
        public const string Scripting = "scripting";
        public const string DataVisualization = "data-visualization";
        public const string LogPanel = "log-panel";
        public const string SerialMonitor = "serial-monitor";
        public const string ProcessMonitor = "process-monitor";
        public const string UsbCapture = "usb-capture";
    }

    public MainWindow()
    {
        InitializeComponent();

        // 获取服务
        _panelManager = App.Services.GetRequiredService<IPanelManager>();
        _configService = App.Services.GetRequiredService<IConfigurationService>();
        var configDirectory = System.IO.Path.GetDirectoryName((_configService as JsonConfigurationService)?.ConfigFilePath) ?? App.DataDirectory;
        _layoutStore = new(System.IO.Path.Combine(configDirectory, "workspace-layout.xml"));

        // 加载并恢复布局状态


        // 创建收发区域视图实例
        _serialCommunicationView = new SerialCommunicationView();
        var communicationViewModel = App.Services.GetRequiredService<SerialCommunicationViewModel>();
        _serialCommunicationView.DataContext = communicationViewModel;

        // 订阅 ViewModel 属性变更以更新状态栏
        communicationViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(communicationViewModel.RxBytes))
            {
                RxBytesText.Text = FormatByteCount(communicationViewModel.RxBytes);
            }
            else if (e.PropertyName == nameof(communicationViewModel.TxBytes))
            {
                TxBytesText.Text = FormatByteCount(communicationViewModel.TxBytes);
            }
        };

        // 设置中央内容
        Workspace.CenterContentElement = _serialCommunicationView;

        // 创建视图实例
        _connectionConfigView = new ConnectionConfigView
        {
            DataContext = App.Services.GetRequiredService<ConnectionConfigViewModel>()
        };

        _commandListView = new CommandListView();

        // 获取 CommandListViewModel 并订阅发送请求事件
        var commandListViewModel = App.Services.GetRequiredService<CommandListViewModel>();
        _commandListView.DataContext = commandListViewModel;

        commandListViewModel.SendDataRequested += (sender, data) =>
        {
            communicationViewModel.SendData(data);
        };

        // 创建自动回复视图
        _autoReplyView = new AutoReplyView(App.Services.GetRequiredService<AutoReplyViewModel>());

        // 创建脚本视图
        _scriptingView = new ScriptingView(App.Services.GetRequiredService<ScriptingViewModel>());

        // 创建数据可视化视图
        var visualizationViewModel = App.Services.GetRequiredService<DataVisualizationViewModel>();
        _dataVisualizationView = new DataVisualizationView(visualizationViewModel);

        // 创建统一日志面板
        var loggingService = App.Services.GetRequiredService<ILoggingService>();
        var persistenceService = App.Services.GetRequiredService<ILogPersistenceService>();
        persistenceService.WriteSessionStart();
        var logPanelViewModel = new LogPanelViewModel(loggingService);
        _logPanelView = new LogPanelView(logPanelViewModel);

        _serialMonitorView = new SerialMonitorView(App.Services.GetRequiredService<SerialMonitorViewModel>());

        // 记录会话启动
        loggingService.Info(Core.Features.Logging.Models.LogSource.System, "应用程序启动");

        // 连接所有数据源到可视化服务
        var visualizationService = App.Services.GetRequiredService<IVisualizationService>();
        var serialPortService = App.Services.GetRequiredService<ISerialPortService>();
        var tcpClientService = App.Services.GetRequiredService<ITcpClientService>();
        var tcpServerService = App.Services.GetRequiredService<ITcpServerService>();
        var udpService = App.Services.GetRequiredService<IUdpService>();

        // 串口数据 → 可视化
        serialPortService.DataReceived += (_, data) => visualizationService.FeedRawData(data);

        // TCP 客户端数据 → 可视化
        tcpClientService.DataReceived += (_, args) => visualizationService.FeedRawData(args.Data);

        // TCP 服务器数据 → 可视化
        tcpServerService.DataReceived += (_, args) => visualizationService.FeedRawData(args.Data);

        // UDP 数据 → 可视化
        udpService.DataReceived += (_, args) => visualizationService.FeedRawData(args.Data);

        // 初始化布局
        InitializeLayout();
        Workspace.OperationFailed += (_, error) => MessageBox.Show(this, error.Message, "工作区操作失败");
        Workspace.DocumentRestoreRequested += (_, id) =>
        {
            if (id.StartsWith("script:", StringComparison.Ordinal)) OpenScriptEditor(id[7..]);
            else if (id == "protocol-definitions") OpenProtocolParser();
            else if (id == "protocol-tx") OpenProtocolBuild();
        };
        Workspace.Manager.LayoutFloatingWindowControlCreated += (_, args) =>
        {
            args.LayoutFloatingWindowControl.PreviewKeyDown += WorkspaceWindow_KeyDown;
        };
        PreviewKeyDown += WorkspaceWindow_KeyDown;

        // 设置所有者窗口（用于浮动窗口）
        var themes = App.Services.GetRequiredService<Services.IThemeService>();
        Workspace.ApplyTheme(themes.CurrentTheme);
        themes.ThemeChanged += OnWorkspaceThemeChanged;
        Closed += (_, _) => themes.ThemeChanged -= OnWorkspaceThemeChanged;

        // 窗口加载完成后恢复浮动面板并检查更新
        Loaded += async (_, _) =>
        {
            if (!_layoutStore.Load(Workspace, _configService.Load().LayoutState,
                (_configService as JsonConfigurationService)?.ConfigFilePath))
                App.Services.GetRequiredService<ILoggingService>().Warning(Core.Features.Logging.Models.LogSource.System,
                    "布局无法恢复，已使用默认布局；原文件保留在 workspace-layout.xml.invalid（如可写）。");

            // 后台检查更新
            await CheckForUpdateInBackgroundAsync();
        };
    }

    /// <summary>
    /// 后台静默检查更新
    /// </summary>
    private async Task CheckForUpdateInBackgroundAsync()
    {
        try
        {
            var updateViewModel = App.Services.GetRequiredService<UpdateViewModel>();

            // 先订阅更新可用事件
            updateViewModel.UpdateAvailable += OnUpdateAvailable;

            // 如果之前已经检查过有更新，直接显示徽章
            if (updateViewModel.HasUpdate)
            {
                ActivityBar.ShowUpdateBadge(true);
                return;
            }

            // 静默检查更新
            await updateViewModel.CheckForUpdateSilentlyAsync();

            // 检查完成后再次检查状态，防止事件未触发的情况
            if (updateViewModel.HasUpdate)
            {
                ActivityBar.ShowUpdateBadge(true);
            }
        }
        catch (Exception ex)
        {
            // 调试输出错误
            System.Diagnostics.Debug.WriteLine($"后台检查更新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新可用事件处理
    /// </summary>
    private void OnUpdateAvailable(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => ActivityBar.ShowUpdateBadge(true));
    }

    private void SaveLayoutState() => _layoutStore.Save(Workspace);

    private void OnWorkspaceThemeChanged(object? sender, Services.ThemeType theme) => Workspace.ApplyTheme(theme);

    private void ResetLayout_Click(object sender, RoutedEventArgs e) => Workspace.ResetLayout();

    private void WorkspaceMenu_Opened(object sender, RoutedEventArgs e)
    {
        var items = new List<System.Windows.Controls.MenuItem>();
        foreach (var panel in _panelManager.Panels)
        {
            var item = new System.Windows.Controls.MenuItem { Header = panel.Title };
            item.Click += (_, _) => Workspace.ShowPanel(panel.Id);
            items.Add(item);
        }
        RefreshMenuItems(WorkspaceMenu, items);
    }

    private void WindowMenu_Opened(object sender, RoutedEventArgs e)
    {
        var items = new List<System.Windows.Controls.MenuItem>();
        var model = Workspace.Manager.Layout.ActiveContent;
        var item = model == null ? null : Workspace.Manager.GetLayoutItemFromModel(model);
        if (item == null)
        {
            RefreshMenuItems(WindowMenu, [new System.Windows.Controls.MenuItem { Header = "请选择页面", IsEnabled = false }]);
            return;
        }
        void Add(string label, ICommand command) => items.Add(
            new System.Windows.Controls.MenuItem { Header = label, Command = command });
        Add("浮动(_F)", item.FloatCommand);
        Add("移到文档区(_D)", item.DockAsDocumentCommand);
        Add("新建水平页签组(_H)", item.NewHorizontalTabGroupCommand);
        Add("新建垂直页签组(_V)", item.NewVerticalTabGroupCommand);
        if (item is AvalonDock.Controls.LayoutAnchorableItem tool)
        {
            Add("停靠(_K)", tool.DockCommand);
            Add("自动隐藏(_A)", tool.AutoHideCommand);
            Add("隐藏(_I)", tool.HideCommand);
        }
        else Add("关闭(_C)", item.CloseCommand);
        RefreshMenuItems(WindowMenu, items);
    }

    private static void RefreshMenuItems(System.Windows.Controls.MenuItem menu, IEnumerable<System.Windows.Controls.MenuItem> items)
    {
        // Keep HasItems true while the submenu is open; clearing first closes the popup.
        var previous = menu.Items.Cast<object>().ToArray();
        foreach (var item in items) menu.Items.Add(item);
        foreach (var item in previous) menu.Items.Remove(item);
    }

    private void InitializeLayout()
    {
        // 设置面板管理器
        Workspace.SetPanelManager(_panelManager);

        // 检查是否已有保存的面板状态
        var savedConnectionConfig = _panelManager.GetPanel(PanelIds.ConnectionConfig);
        var savedCommandList = _panelManager.GetPanel(PanelIds.CommandList);
        var savedAutoReply = _panelManager.GetPanel(PanelIds.AutoReply);
        var savedScripting = _panelManager.GetPanel(PanelIds.Scripting);
        var savedSerialMonitor = _panelManager.GetPanel(PanelIds.SerialMonitor);

        // 添加连接配置面板（默认在左侧，可自由移动）
        Workspace.AddPanel(
            PanelIds.ConnectionConfig,
            "连接配置",
            _connectionConfigView,
            savedConnectionConfig?.Zone ?? PanelZone.Left,
            isMovable: true,
            order: savedConnectionConfig?.Order ?? 0
        );

        // 添加指令列表面板（可移动，使用保存的状态）
        Workspace.AddPanel(
            PanelIds.CommandList,
            "指令列表",
            _commandListView,
            savedCommandList?.Zone ?? PanelZone.Right,
            isMovable: true,
            order: savedCommandList?.Order ?? 0
        );

        // 添加自动回复面板（可移动，默认在右侧）
        Workspace.AddPanel(
            PanelIds.AutoReply,
            "自动回复",
            _autoReplyView,
            savedAutoReply?.Zone ?? PanelZone.Right,
            isMovable: true,
            order: savedAutoReply?.Order ?? 1
        );

        // 添加脚本面板（可移动，默认在右侧）
        Workspace.AddPanel(
            PanelIds.Scripting,
            "脚本",
            _scriptingView,
            savedScripting?.Zone ?? PanelZone.Right,
            isMovable: true,
            order: savedScripting?.Order ?? 2
        );

        Workspace.AddPanel(
            PanelIds.SerialMonitor,
            "串口监控",
            _serialMonitorView,
            savedSerialMonitor?.Zone ?? PanelZone.Bottom,
            isMovable: true,
            order: savedSerialMonitor?.Order ?? 0
        );

        // 添加数据可视化面板（可移动，默认在底部）
        Workspace.AddPanel(PanelIds.ProcessMonitor, "进程串口监控（API Hook）",
            new Features.ProcessMonitor.Views.ProcessMonitorView
            { DataContext = App.Services.GetRequiredService<Core.Features.ProcessMonitor.ViewModels.ProcessMonitorViewModel>() },
            _panelManager.GetPanel(PanelIds.ProcessMonitor)?.Zone ?? PanelZone.Bottom,
            isMovable: true, order: _panelManager.GetPanel(PanelIds.ProcessMonitor)?.Order ?? 3);
        Workspace.AddPanel(PanelIds.UsbCapture, "USB 抓包（USBPcap）",
            new Features.UsbCapture.Views.UsbCaptureView
            { DataContext = App.Services.GetRequiredService<Core.Features.UsbCapture.ViewModels.UsbCaptureViewModel>() },
            _panelManager.GetPanel(PanelIds.UsbCapture)?.Zone ?? PanelZone.Bottom,
            isMovable: true, order: _panelManager.GetPanel(PanelIds.UsbCapture)?.Order ?? 4);

        var savedVisualization = _panelManager.GetPanel(PanelIds.DataVisualization);
        Workspace.AddPanel(
            PanelIds.DataVisualization,
            "数据可视化",
            _dataVisualizationView,
            savedVisualization?.Zone ?? PanelZone.Bottom,
            isMovable: true,
            order: savedVisualization?.Order ?? 1
        );

        // 添加统一日志面板（可移动，默认在底部）
        var savedLogPanel = _panelManager.GetPanel(PanelIds.LogPanel);
        Workspace.AddPanel(
            PanelIds.LogPanel,
            "日志",
            _logPanelView,
            savedLogPanel?.Zone ?? PanelZone.Bottom,
            isMovable: true,
            order: savedLogPanel?.Order ?? 2
        );

        // 设置面板列表提供器（保留兼容性）
        ActivityBar.SetPanelsProvider(() => _panelManager.Panels

            .Select(p => (p.Id, p.Title, p.IsVisible)));

        // 订阅面板可见性切换事件
        ActivityBar.PanelVisibilityToggled += (sender, args) =>
        {
            Workspace.ShowPanel(args.PanelId);
        };

        // 订阅校验计算器按钮点击事件
        ActivityBar.ChecksumCalculatorClicked += (sender, args) =>
        {
            OpenChecksumCalculator();
        };

        // 订阅协议解析器按钮点击事件
        ActivityBar.ProtocolParserClicked += (sender, args) =>
        {
            OpenProtocolParser();
        };

        // 订阅更新按钮点击事件
        ActivityBar.UpdateClicked += (sender, args) =>
        {
            OpenUpdateWindow();
        };

        // 订阅设置按钮点击事件
        ActivityBar.SettingsClicked += (sender, args) =>
        {
            OpenSettingsWindow();
        };

    }

    /// <summary>
    /// 打开校验和计算器窗口
    /// </summary>
    private void OpenChecksumCalculator()
    {
        var viewModel = App.Services.GetRequiredService<ChecksumCalculatorViewModel>();
        var window = new ChecksumCalculatorWindow(viewModel)
        {
            Owner = this
        };

        // 设置回调以获取发送帧数据（返回原始数据和模式）
        window.GetSendFrameData = () =>
        {
            var communicationVm = _serialCommunicationView.DataContext as SerialCommunicationViewModel;
            if (communicationVm != null && !string.IsNullOrEmpty(communicationVm.SendText))
            {
                return (communicationVm.SendText, communicationVm.IsHexSendMode);
            }
            return (null, true);
        };

        // 设置回调以附加数据到发送帧（强制切换到 Hex 模式）
        window.AppendToSendFrame = (data) =>
        {
            var communicationVm = _serialCommunicationView.DataContext as SerialCommunicationViewModel;
            if (communicationVm != null)
            {
                // 强制切换到 Hex 模式并转换现有内容
                if (!communicationVm.IsHexSendMode)
                {
                    communicationVm.SwitchToHexModeWithConversion();
                }

                var hexString = HexHelper.BytesToHexString(data);
                communicationVm.SendText = string.IsNullOrEmpty(communicationVm.SendText)
                    ? hexString
                    : $"{communicationVm.SendText} {hexString}";
            }
        };

        window.ShowDialog();
    }

    /// <summary>
    /// 打开协议解析器窗口
    /// </summary>
    private void OpenProtocolParser()
    {
        ProtocolDefinitionView? view = null;
        Workspace.OpenDocument("protocol-definitions", "协议管理",
            () => view = new ProtocolDefinitionView(App.Services.GetRequiredService<ProtocolParserViewModel>()),
            () => view!.RequestCloseAsync(), () => view!.Release());
    }

    public void OpenScriptEditor(string scriptId)
    {
        ScriptEditorView? view = null;
        var manager = App.Services.GetRequiredService<Core.Features.Scripting.Services.IScriptManager>();
        var script = manager.GetAllScripts().FirstOrDefault(s => s.Id == scriptId);
        if (script == null) return;
        Workspace.OpenDocument("script:" + scriptId, script.Name, () =>
        {
            // An editor owns its buffer; selecting a different script in the tool pane cannot replace it.
            var vm = new ScriptingViewModel(
                App.Services.GetRequiredService<Core.Features.Scripting.Services.IScriptEngine>(), manager,
                App.Services.GetRequiredService<Core.Features.Scripting.Services.IScriptApiBridge>());
            vm.SelectedScript = script;
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ScriptingViewModel.IsEditorDirty) && !vm.IsEditorDirty)
                    ((ScriptingViewModel)_scriptingView.DataContext).RefreshSavedScript(scriptId);
            };
            return view = new ScriptEditorView(vm, App.Services.GetRequiredService<IProtocolParserService>());
        }, () => view!.RequestCloseAsync(), () => view!.Release());
    }

    public void OpenProtocolParse(string hex)
    {
        // Separate records can be compared without replacing another page's working input.
        Workspace.OpenDocument("protocol-rx:" + Guid.NewGuid().ToString("N"), "接收帧解析",
            () => new RxProtocolParseView(App.Services.GetRequiredService<IProtocolParserService>(), hex));
    }

    public void OpenProtocolBuild() => Workspace.OpenDocument("protocol-tx", "发送帧组帧",
        () => new TxProtocolBuildView(App.Services.GetRequiredService<IProtocolParserService>(),
            (SerialCommunicationViewModel)_serialCommunicationView.DataContext));

    /// <summary>
    /// 打开更新窗口
    /// </summary>
    private void OpenUpdateWindow()
    {
        // 隐藏更新徽章
        ActivityBar.ShowUpdateBadge(false);

        var viewModel = App.Services.GetRequiredService<UpdateViewModel>();
        var window = new UpdateWindow(viewModel)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    /// <summary>
    /// 打开设置窗口
    /// </summary>
    private void OpenSettingsWindow()
    {
        var logDir = System.IO.Path.Combine(App.DataDirectory, "logs");
        var viewModel = new SettingsViewModel(
            _configService,
            App.Services.GetRequiredService<Core.Features.Update.Services.IVersionService>(),
            _panelManager,
            logDir,
            App.Services.GetRequiredService<Core.Features.Driver.Services.DriverLifecycleService>());

        var window = new SettingsWindow(viewModel)
        {
            Owner = this
        };

        // 订阅面板可见性切换事件
        window.PanelVisibilityToggled += (_, panelId) =>
        {
            Workspace.TogglePanelVisibility(panelId);
        };

        viewModel.PanelVisibilityToggled += (_, panelId) =>
        {
            Workspace.TogglePanelVisibility(panelId);
        };

        window.ShowDialog();
    }

    /// <summary>
    /// 打开调试工具窗口
    /// </summary>
    private void OpenDebugToolsWindow()
    {
        var config = _configService.Load();
        if (!config.DebugConfig.IsDebugModeEnabled)
            return;

        var loggingService = App.Services.GetRequiredService<ILoggingService>();
        var viewModel = new DebugToolsViewModel(loggingService);
        var window = new DebugToolsWindow(viewModel)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            OpenDebugToolsWindow();
            e.Handled = true;
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closeApproved) return;
        e.Cancel = true;
        if (_closeInProgress) return;
        _closeInProgress = true;
        try
        {
            if (!await Workspace.CanCloseDocumentsAsync()) return;
            SaveLayoutState();
            var logging = App.Services.GetRequiredService<ILoggingService>();
            logging.Info(Core.Features.Logging.Models.LogSource.System, "应用程序关闭");
            var persistence = App.Services.GetRequiredService<ILogPersistenceService>();
            persistence.WriteSessionEnd();
            persistence.Flush();
            Workspace.ReleaseDocuments();
            await App.DisposeServicesAsync();
            _closeApproved = true;
            // Defer the second close until the current Closing event has completed.
            _ = Dispatcher.BeginInvoke(Close);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"无法关闭工作区：{error.Message}", "工作区");
        }
        finally { _closeInProgress = false; }
    }

    private void WorkspaceWindow_KeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F12) { OpenDebugToolsWindow(); e.Handled = true; }
        else if (key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Workspace.ActivateNextPage(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (key == Key.V && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            Activate(); WorkspaceMenu.Focus(); WorkspaceMenu.IsSubmenuOpen = true; e.Handled = true;
        }
        else if (key == Key.W && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            Activate(); WindowMenu.Focus(); WindowMenu.IsSubmenuOpen = true; e.Handled = true;
        }
    }

    /// <summary>
    /// 重置统计计数器
    /// </summary>
    private void ResetCounters_Click(object sender, RoutedEventArgs e)
    {
        if (_serialCommunicationView.DataContext is SerialCommunicationViewModel viewModel)
        {
            viewModel.ResetCountersCommand.Execute(null);
        }
    }

    /// <summary>
    /// 格式化字节数显示
    /// </summary>
    private static string FormatByteCount(long bytes)
    {
        if (bytes < 1024)
            return bytes.ToString();
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1}K";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1}M";
        return $"{bytes / (1024.0 * 1024 * 1024):F2}G";
    }
}
