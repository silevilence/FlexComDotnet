using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;
using FlexComDotnet.Core.Features.Checksum.ViewModels;
using FlexComDotnet.Core.Features.AutoReply.ViewModels;
using FlexComDotnet.Core.Features.Network.ViewModels;
using FlexComDotnet.Core.Features.Update.ViewModels;
using FlexComDotnet.Features.Serial.Views;
using FlexComDotnet.Features.Network.Views;
using FlexComDotnet.Features.Checksum.Views;
using FlexComDotnet.Features.AutoReply.Views;
using FlexComDotnet.Features.Update.Views;
using FlexComDotnet.Features.Scripting.Views;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using static FlexComDotnet.Features.Layout.Controls.ActivityBar;

namespace FlexComDotnet;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IPanelManager _panelManager;
    private readonly IConfigurationService _configService;
    private readonly ConnectionConfigView _connectionConfigView;
    private readonly CommandListView _commandListView;
    private readonly AutoReplyView _autoReplyView;
    private readonly ScriptingView _scriptingView;
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
    }

    public MainWindow()
    {
        InitializeComponent();
        
        // 获取服务
        _panelManager = App.Services.GetRequiredService<IPanelManager>();
        _configService = App.Services.GetRequiredService<IConfigurationService>();
        
        // 加载并恢复布局状态
        LoadLayoutState();
        
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
        MultiZoneLayout.CenterContentElement = _serialCommunicationView;
        
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

        // 初始化布局
        InitializeLayout();
        
        // 设置所有者窗口（用于浮动窗口）
        MultiZoneLayout.SetOwnerWindow(this);
        
        // 窗口加载完成后恢复浮动面板并检查更新
        Loaded += async (_, _) =>
        {
            MultiZoneLayout.RestoreFloatingPanels();
            
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

    private void LoadLayoutState()
    {
        var config = _configService.Load();
        if (config.LayoutState.Panels.Count > 0)
        {
            _panelManager.RestoreLayoutState(config.LayoutState);
        }
    }

    private void SaveLayoutState()
    {
        var config = _configService.Load();
        config.LayoutState = _panelManager.GetLayoutState();
        _configService.Save(config);
    }

    private void InitializeLayout()
    {
        // 设置面板管理器
        MultiZoneLayout.SetPanelManager(_panelManager);

        // 检查是否已有保存的面板状态
        var savedConnectionConfig = _panelManager.GetPanel(PanelIds.ConnectionConfig);
        var savedCommandList = _panelManager.GetPanel(PanelIds.CommandList);
        var savedAutoReply = _panelManager.GetPanel(PanelIds.AutoReply);
        var savedScripting = _panelManager.GetPanel(PanelIds.Scripting);

        // 添加连接配置面板（固定在左侧，不可移动）
        MultiZoneLayout.AddPanel(
            PanelIds.ConnectionConfig,
            "连接配置",
            _connectionConfigView,
            savedConnectionConfig?.Zone ?? PanelZone.Left,
            isMovable: false,  // 连接配置面板固定不可移动
            order: savedConnectionConfig?.Order ?? 0
        );

        // 添加指令列表面板（可移动，使用保存的状态）
        MultiZoneLayout.AddPanel(
            PanelIds.CommandList,
            "指令列表",
            _commandListView,
            savedCommandList?.Zone ?? PanelZone.Right,
            isMovable: true,
            order: savedCommandList?.Order ?? 0
        );

        // 添加自动回复面板（可移动，默认在右侧）
        MultiZoneLayout.AddPanel(
            PanelIds.AutoReply,
            "自动回复",
            _autoReplyView,
            savedAutoReply?.Zone ?? PanelZone.Right,
            isMovable: true,
            order: savedAutoReply?.Order ?? 1
        );

        // 添加脚本面板（可移动，默认在右侧）
        MultiZoneLayout.AddPanel(
            PanelIds.Scripting,
            "脚本",
            _scriptingView,
            savedScripting?.Zone ?? PanelZone.Right,
            isMovable: true,
            order: savedScripting?.Order ?? 2
        );

        // 同步 ActivityBar 状态
        ActivityBar.IsLeftPanelChecked = !_panelManager.IsZoneCollapsed(PanelZone.Left);
        ActivityBar.IsRightPanelChecked = !_panelManager.IsZoneCollapsed(PanelZone.Right);
        ActivityBar.IsBottomPanelChecked = !_panelManager.IsZoneCollapsed(PanelZone.Bottom);
        
        // 设置面板列表提供器（用于面板管理菜单，过滤掉不可移动的面板）
        ActivityBar.SetPanelsProvider(() => _panelManager.Panels
            .Where(p => p.Id != PanelIds.ConnectionConfig)
            .Select(p => (p.Id, p.Title, p.IsVisible)));
        
        // 订阅面板可见性切换事件
        ActivityBar.PanelVisibilityToggled += (sender, args) =>
        {
            MultiZoneLayout.TogglePanelVisibility(args.PanelId);
        };
        
        // 订阅面板可见性变更事件
        MultiZoneLayout.PanelVisibilityChanged += (sender, args) =>
        {
            // 可以在这里更新 ActivityBar 或其他 UI
        };
        
        // 订阅校验计算器按钮点击事件
        ActivityBar.ChecksumCalculatorClicked += (sender, args) =>
        {
            OpenChecksumCalculator();
        };

        // 订阅更新按钮点击事件
        ActivityBar.UpdateClicked += (sender, args) =>
        {
            OpenUpdateWindow();
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

    private void ActivityBar_ZoneToggled(object? sender, ZoneToggleEventArgs e)
    {
        MultiZoneLayout.SetZoneCollapsed(e.Zone, !e.IsVisible);
    }

    private void MultiZoneLayout_ZoneSizeChanged(object? sender, (PanelZone Zone, double Size) e)
    {
        // 区域尺寸变更时自动保存
        // 面板管理器已自动更新尺寸
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 关闭所有浮动窗口
        MultiZoneLayout.CloseAllFloatingWindows();
        
        // 保存布局状态
        SaveLayoutState();
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
