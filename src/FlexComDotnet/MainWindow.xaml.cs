using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;
using FlexComDotnet.Core.Features.Checksum.ViewModels;
using FlexComDotnet.Core.Features.AutoReply.ViewModels;
using FlexComDotnet.Features.Serial.Views;
using FlexComDotnet.Features.Checksum.Views;
using FlexComDotnet.Features.AutoReply.Views;
using static FlexComDotnet.Features.Layout.Controls.ActivityBar;

namespace FlexComDotnet;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IPanelManager _panelManager;
    private readonly IConfigurationService _configService;
    private readonly SerialConfigView _serialConfigView;
    private readonly CommandListView _commandListView;
    private readonly AutoReplyView _autoReplyView;
    private readonly SerialCommunicationView _serialCommunicationView;

    /// <summary>
    /// 面板 ID 常量
    /// </summary>
    private static class PanelIds
    {
        public const string SerialConfig = "serial-config";
        public const string CommandList = "command-list";
        public const string AutoReply = "auto-reply";
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
        
        // 设置中央内容
        MultiZoneLayout.CenterContentElement = _serialCommunicationView;
        
        // 创建视图实例
        _serialConfigView = new SerialConfigView
        {
            DataContext = App.Services.GetRequiredService<SerialConfigViewModel>()
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

        // 初始化布局
        InitializeLayout();
        
        // 设置所有者窗口（用于浮动窗口）
        MultiZoneLayout.SetOwnerWindow(this);
        
        // 窗口加载完成后恢复浮动面板
        Loaded += (_, _) => MultiZoneLayout.RestoreFloatingPanels();
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
        var savedSerialConfig = _panelManager.GetPanel(PanelIds.SerialConfig);
        var savedCommandList = _panelManager.GetPanel(PanelIds.CommandList);
        var savedAutoReply = _panelManager.GetPanel(PanelIds.AutoReply);

        // 添加串口配置面板（固定在左侧，不可移动）
        MultiZoneLayout.AddPanel(
            PanelIds.SerialConfig,
            "串口配置",
            _serialConfigView,
            savedSerialConfig?.Zone ?? PanelZone.Left,
            isMovable: false,  // 串口配置面板固定不可移动
            order: savedSerialConfig?.Order ?? 0
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

        // 同步 ActivityBar 状态
        ActivityBar.IsLeftPanelChecked = !_panelManager.IsZoneCollapsed(PanelZone.Left);
        ActivityBar.IsRightPanelChecked = !_panelManager.IsZoneCollapsed(PanelZone.Right);
        ActivityBar.IsBottomPanelChecked = !_panelManager.IsZoneCollapsed(PanelZone.Bottom);
        
        // 设置面板列表提供器（用于面板管理菜单）
        ActivityBar.SetPanelsProvider(() => _panelManager.Panels.Select(p => (p.Id, p.Title, p.IsVisible)));
        
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
}
