using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Core.Features.Layout.Models;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>
/// VS Code 风格的活动栏控件
/// </summary>
public partial class ActivityBar : UserControl
{
    /// <summary>
    /// 区域切换事件参数
    /// </summary>
    public class ZoneToggleEventArgs : EventArgs
    {
        public PanelZone Zone { get; }
        public bool IsVisible { get; }

        public ZoneToggleEventArgs(PanelZone zone, bool isVisible)
        {
            Zone = zone;
            IsVisible = isVisible;
        }
    }

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
    /// 区域切换事件
    /// </summary>
    public event EventHandler<ZoneToggleEventArgs>? ZoneToggled;

    /// <summary>
    /// 面板可见性切换事件
    /// </summary>
    public event EventHandler<PanelVisibilityEventArgs>? PanelVisibilityToggled;

    private readonly ContextMenu _panelMenu;
    private Func<IEnumerable<(string Id, string Title, bool IsVisible)>>? _getPanelsFunc;

    public ActivityBar()
    {
        InitializeComponent();
        _panelMenu = new ContextMenu();
        SettingsButton.ContextMenu = _panelMenu;
    }

    /// <summary>
    /// 设置获取面板列表的委托
    /// </summary>
    public void SetPanelsProvider(Func<IEnumerable<(string Id, string Title, bool IsVisible)>> getPanelsFunc)
    {
        _getPanelsFunc = getPanelsFunc;
    }

    /// <summary>
    /// 设置左侧面板按钮的选中状态
    /// </summary>
    public bool IsLeftPanelChecked
    {
        get => LeftPanelButton.IsChecked == true;
        set => LeftPanelButton.IsChecked = value;
    }

    /// <summary>
    /// 设置右侧面板按钮的选中状态
    /// </summary>
    public bool IsRightPanelChecked
    {
        get => RightPanelButton.IsChecked == true;
        set => RightPanelButton.IsChecked = value;
    }

    /// <summary>
    /// 设置底部面板按钮的选中状态
    /// </summary>
    public bool IsBottomPanelChecked
    {
        get => BottomPanelButton.IsChecked == true;
        set => BottomPanelButton.IsChecked = value;
    }

    private void LeftPanelButton_Checked(object sender, RoutedEventArgs e)
    {
        ZoneToggled?.Invoke(this, new ZoneToggleEventArgs(PanelZone.Left, true));
    }

    private void LeftPanelButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ZoneToggled?.Invoke(this, new ZoneToggleEventArgs(PanelZone.Left, false));
    }

    private void RightPanelButton_Checked(object sender, RoutedEventArgs e)
    {
        ZoneToggled?.Invoke(this, new ZoneToggleEventArgs(PanelZone.Right, true));
    }

    private void RightPanelButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ZoneToggled?.Invoke(this, new ZoneToggleEventArgs(PanelZone.Right, false));
    }

    private void BottomPanelButton_Checked(object sender, RoutedEventArgs e)
    {
        ZoneToggled?.Invoke(this, new ZoneToggleEventArgs(PanelZone.Bottom, true));
    }

    private void BottomPanelButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ZoneToggled?.Invoke(this, new ZoneToggleEventArgs(PanelZone.Bottom, false));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // 更新面板菜单
        UpdatePanelMenu();
        
        // 显示面板菜单
        _panelMenu.PlacementTarget = SettingsButton;
        _panelMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
        _panelMenu.IsOpen = true;
    }

    private void UpdatePanelMenu()
    {
        _panelMenu.Items.Clear();

        // 添加标题
        var header = new MenuItem { Header = "面板管理", IsEnabled = false, FontWeight = FontWeights.Bold };
        _panelMenu.Items.Add(header);
        _panelMenu.Items.Add(new Separator());

        // 获取面板列表
        var panels = _getPanelsFunc?.Invoke();
        if (panels != null)
        {
            foreach (var (id, title, isVisible) in panels)
            {
                var menuItem = new MenuItem
                {
                    Header = title,
                    IsCheckable = true,
                    IsChecked = isVisible,
                    Tag = id
                };
                menuItem.Click += (s, _) =>
                {
                    if (s is MenuItem item && item.Tag is string panelId)
                    {
                        PanelVisibilityToggled?.Invoke(this, new PanelVisibilityEventArgs(panelId));
                    }
                };
                _panelMenu.Items.Add(menuItem);
            }
        }

        _panelMenu.Items.Add(new Separator());

        // 添加全部显示/隐藏选项
        var showAllItem = new MenuItem { Header = "显示全部" };
        showAllItem.Click += (_, _) =>
        {
            var allPanels = _getPanelsFunc?.Invoke();
            if (allPanels != null)
            {
                foreach (var (id, _, isVisible) in allPanels)
                {
                    if (!isVisible)
                    {
                        PanelVisibilityToggled?.Invoke(this, new PanelVisibilityEventArgs(id));
                    }
                }
            }
        };
        _panelMenu.Items.Add(showAllItem);
    }
}
