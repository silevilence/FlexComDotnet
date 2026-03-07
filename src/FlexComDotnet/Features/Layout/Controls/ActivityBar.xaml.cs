using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>
/// VS Code 风格的活动栏控件 (科技感设计)
/// </summary>
public partial class ActivityBar : UserControl
{
    /// <summary>
    /// 面板可见性切换事件参数
    /// </summary>
    public class PanelVisibilityEventArgs : EventArgs
    {
        public string PanelId { get; }

        public PanelVisibilityEventArgs(string panelId)
        {
            PanelId = panelId;
        }
    }

    /// <summary>
    /// 面板可见性切换事件
    /// </summary>
    public event EventHandler<PanelVisibilityEventArgs>? PanelVisibilityToggled;

    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    public event EventHandler? SettingsClicked;

    private readonly IThemeService? _themeService;

    public ActivityBar()
    {
        InitializeComponent();

        // 获取主题服务
        _themeService = App.Services?.GetService<IThemeService>();
        
        // 初始化主题按钮状态
        if (_themeService != null)
        {
            UpdateThemeIcon(_themeService.CurrentMode);
            _themeService.ModeChanged += OnThemeModeChanged;
        }
    }

    /// <summary>
    /// 主题模式变化回调
    /// </summary>
    private void OnThemeModeChanged(object? sender, Services.ThemeMode mode)
    {
        Dispatcher.Invoke(() => UpdateThemeIcon(mode));
    }

    /// <summary>
    /// 更新主题图标显示
    /// </summary>
    private void UpdateThemeIcon(Services.ThemeMode mode)
    {
        SunIcon.Visibility = mode == Services.ThemeMode.Light ? Visibility.Visible : Visibility.Collapsed;
        MoonIcon.Visibility = mode == Services.ThemeMode.Dark ? Visibility.Visible : Visibility.Collapsed;
        AutoIcon.Visibility = mode == Services.ThemeMode.System ? Visibility.Visible : Visibility.Collapsed;

        ThemeCycleButton.ToolTip = mode switch
        {
            Services.ThemeMode.Light => "当前: 浅色模式\n点击切换到: 深色模式",
            Services.ThemeMode.Dark => "当前: 深色模式\n点击切换到: 跟随系统",
            Services.ThemeMode.System => "当前: 跟随系统\n点击切换到: 浅色模式",
            _ => "切换主题"
        };
    }

    /// <summary>
    /// 设置获取面板列表的委托（保留以备兼容）
    /// </summary>
    public void SetPanelsProvider(Func<IEnumerable<(string Id, string Title, bool IsVisible)>> getPanelsFunc)
    {
        // 面板管理已迁移至设置窗口，此方法保留兼容性
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ThemeCycleButton_Click(object sender, RoutedEventArgs e)
    {
        _themeService?.CycleMode();
    }

    /// <summary>
    /// 协议解析器按钮点击事件
    /// </summary>
    public event EventHandler? ProtocolParserClicked;

    private void ProtocolParserButton_Click(object sender, RoutedEventArgs e)
    {
        ProtocolParserClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 校验计算器按钮点击事件
    /// </summary>
    public event EventHandler? ChecksumCalculatorClicked;

    private void ChecksumCalculatorButton_Click(object sender, RoutedEventArgs e)
    {
        ChecksumCalculatorClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 更新按钮点击事件
    /// </summary>
    public event EventHandler? UpdateClicked;

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 显示或隐藏更新可用状态
    /// </summary>
    public void ShowUpdateBadge(bool hasUpdate)
    {
        Dispatcher.Invoke(() =>
        {
            NoUpdateIcon.Visibility = hasUpdate ? Visibility.Collapsed : Visibility.Visible;
            HasUpdateIcon.Visibility = hasUpdate ? Visibility.Visible : Visibility.Collapsed;
            UpdateButton.ToolTip = hasUpdate ? "有新版本可用，点击查看" : "检查更新";
        });
    }
}
