using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>
/// 多区域标签式布局控件
/// </summary>
public partial class MultiZoneLayout : UserControl
{
    private readonly Dictionary<string, UIElement> _panelContents = [];
    private readonly Dictionary<string, FloatingPanelWindow> _floatingWindows = [];
    private IPanelManager? _panelManager;
    private Window? _ownerWindow;
    private bool _isFloatingInProgress;
    private bool _isSavingLayout;

    #region Dependency Properties

    /// <summary>
    /// 中央内容依赖属性
    /// </summary>
    public static readonly DependencyProperty CenterContentProperty =
        DependencyProperty.Register(
            nameof(CenterContentElement),
            typeof(object),
            typeof(MultiZoneLayout),
            new PropertyMetadata(null, OnCenterContentChanged));

    /// <summary>
    /// 中央区域内容
    /// </summary>
    public object CenterContentElement
    {
        get => GetValue(CenterContentProperty);
        set => SetValue(CenterContentProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// 区域尺寸变更事件
    /// </summary>
    public event EventHandler<(PanelZone Zone, double Size)>? ZoneSizeChanged;

    /// <summary>
    /// 面板可见性变更事件
    /// </summary>
    public event EventHandler<(string PanelId, bool IsVisible)>? PanelVisibilityChanged;

    #endregion

    public MultiZoneLayout()
    {
        InitializeComponent();
    }

    private static void OnCenterContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MultiZoneLayout layout)
        {
            layout.CenterContent.Content = e.NewValue;
        }
    }

    /// <summary>
    /// 设置面板管理器
    /// </summary>
    public void SetPanelManager(IPanelManager panelManager)
    {
        if (_panelManager != null)
        {
            _panelManager.LayoutChanged -= OnLayoutChanged;
        }

        _panelManager = panelManager;
        _panelManager.LayoutChanged += OnLayoutChanged;

        RefreshLayout();
    }

    /// <summary>
    /// 添加面板
    /// </summary>
    public void AddPanel(string panelId, string title, UIElement content, PanelZone zone, bool isMovable = true, int order = 0)
    {
        // 保存内容引用
        _panelContents[panelId] = content;

        // 注册到面板管理器
        _panelManager?.RegisterPanel(new PanelInfo
        {
            Id = panelId,
            Title = title,
            Zone = zone,
            Order = order,
            IsExpanded = true,
            IsMovable = isMovable,
            IsVisible = true
        });

        RefreshLayout();
    }

    /// <summary>
    /// 移动面板到指定区域
    /// </summary>
    public void MovePanel(string panelId, PanelZone targetZone)
    {
        _panelManager?.MovePanel(panelId, targetZone);
    }

    /// <summary>
    /// 设置区域折叠状态
    /// </summary>
    public void SetZoneCollapsed(PanelZone zone, bool isCollapsed)
    {
        _panelManager?.SetZoneCollapsed(zone, isCollapsed);
    }

    /// <summary>
    /// 获取区域是否折叠
    /// </summary>
    public bool IsZoneCollapsed(PanelZone zone)
    {
        return _panelManager?.IsZoneCollapsed(zone) ?? false;
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        if (_isFloatingInProgress || _isSavingLayout) return;
        Dispatcher.Invoke(RefreshLayout);
    }

    /// <summary>
    /// 刷新整个布局：重建标签栏和内容区域
    /// </summary>
    private void RefreshLayout()
    {
        if (_panelManager == null) return;

        RefreshZone(PanelZone.Left);
        RefreshZone(PanelZone.Right);
        RefreshZone(PanelZone.Bottom);
    }

    /// <summary>
    /// 刷新指定区域的标签栏和内容
    /// </summary>
    private void RefreshZone(PanelZone zone)
    {
        if (_panelManager == null) return;

        var panels = _panelManager.GetPanelsInZone(zone);
        var visiblePanels = panels.Where(p => p.IsVisible && !p.IsFloating).ToList();
        var activePanel = _panelManager.GetActivePanelInZone(zone);

        var (tabBar, contentPresenter, zoneBorder, tabBarBorder, splitter) = GetZoneElements(zone);
        if (tabBar == null) return;

        // 重建标签栏
        tabBar.Children.Clear();
        foreach (var panelInfo in visiblePanels)
        {
            var tabButton = CreateTabButton(panelInfo, zone, activePanel?.Id == panelInfo.Id);
            tabBar.Children.Add(tabButton);
        }

        // 更新内容区域
        if (contentPresenter != null)
        {
            contentPresenter.Content = null;

            if (activePanel != null && _panelContents.TryGetValue(activePanel.Id, out var content))
            {
                // 从浮动窗口移除内容（如果有）
                if (_floatingWindows.TryGetValue(activePanel.Id, out var floatingWindow))
                {
                    floatingWindow.PanelContent = null;
                    floatingWindow.Hide();
                }

                // 确保内容不在其他容器中
                if (content is FrameworkElement fe && fe.Parent is ContentPresenter oldPresenter && oldPresenter != contentPresenter)
                {
                    oldPresenter.Content = null;
                }

                contentPresenter.Content = content;
            }
        }

        UpdateZoneVisibility(zone, visiblePanels.Count, activePanel != null);
    }

    /// <summary>
    /// 创建标签按钮
    /// </summary>
    private Button CreateTabButton(PanelInfo panelInfo, PanelZone zone, bool isActive)
    {
        var button = new Button
        {
            Tag = panelInfo.Id,
            ToolTip = panelInfo.Title
        };

        if (zone == PanelZone.Bottom)
        {
            button.Style = (Style)FindResource("HorizontalTabButtonStyle");
            button.Content = CreateHorizontalTabContent(panelInfo.Title, isActive);
        }
        else
        {
            button.Style = (Style)FindResource("VerticalTabButtonStyle");
            button.Content = CreateVerticalTabContent(panelInfo.Title, isActive);
        }

        if (isActive)
        {
            button.Background = (Brush)FindResource("ActiveTabBrush");
            button.Foreground = (Brush)FindResource("TextPrimaryBrush");
        }

        button.Click += (_, _) =>
        {
            _panelManager?.ActivatePanelInZone(panelInfo.Id);
        };

        button.ContextMenu = CreateTabContextMenu(panelInfo);

        return button;
    }

    /// <summary>
    /// 创建竖向标签内容（文字旋转90度）
    /// </summary>
    private static UIElement CreateVerticalTabContent(string title, bool isActive)
    {
        var grid = new Grid();

        if (isActive)
        {
            var indicator = new Border
            {
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(-4, 0, 0, 0)
            };
            indicator.SetResourceReference(Border.BackgroundProperty, "ActiveTabIndicatorBrush");
            grid.Children.Add(indicator);
        }

        var textBlock = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            RenderTransformOrigin = new Point(0.5, 0.5),
            LayoutTransform = new RotateTransform(90),
            Margin = new Thickness(4, 6, 4, 6)
        };

        grid.Children.Add(textBlock);
        return grid;
    }

    /// <summary>
    /// 创建横向标签内容
    /// </summary>
    private static UIElement CreateHorizontalTabContent(string title, bool isActive)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });

        var textBlock = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
        Grid.SetRow(textBlock, 0);
        grid.Children.Add(textBlock);

        if (isActive)
        {
            var indicator = new Border
            {
                Height = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            indicator.SetResourceReference(Border.BackgroundProperty, "ActiveTabIndicatorBrush");
            Grid.SetRow(indicator, 1);
            grid.Children.Add(indicator);
        }

        return grid;
    }

    /// <summary>
    /// 创建标签右键菜单
    /// </summary>
    private ContextMenu CreateTabContextMenu(PanelInfo panelInfo)
    {
        var contextMenu = new ContextMenu();

        if (panelInfo.IsMovable)
        {
            foreach (var zone in new[] { PanelZone.Left, PanelZone.Right, PanelZone.Bottom })
            {
                if (zone == panelInfo.Zone) continue;

                var zoneName = zone switch
                {
                    PanelZone.Left => "左侧",
                    PanelZone.Right => "右侧",
                    PanelZone.Bottom => "底部",
                    _ => zone.ToString()
                };

                var moveItem = new MenuItem { Header = $"移动到{zoneName}" };
                var targetZone = zone;
                moveItem.Click += (_, _) => _panelManager?.MovePanel(panelInfo.Id, targetZone);
                contextMenu.Items.Add(moveItem);
            }

            contextMenu.Items.Add(new Separator());

            var floatItem = new MenuItem { Header = "弹出为独立窗口" };
            floatItem.Click += (_, _) => FloatPanel(panelInfo.Id, new Point(0, 0));
            contextMenu.Items.Add(floatItem);

            contextMenu.Items.Add(new Separator());
        }

        var hideItem = new MenuItem { Header = "隐藏面板" };
        hideItem.Click += (_, _) => HidePanel(panelInfo.Id);
        contextMenu.Items.Add(hideItem);

        return contextMenu;
    }

    /// <summary>
    /// 获取区域对应的 UI 元素
    /// </summary>
    private (StackPanel? tabBar, ContentPresenter? contentPresenter, Border? zoneBorder, Border? tabBarBorder, GridSplitter? splitter) GetZoneElements(PanelZone zone)
    {
        return zone switch
        {
            PanelZone.Left => (LeftTabBar, LeftContentPresenter, LeftZoneBorder, LeftTabBarBorder, LeftSplitter),
            PanelZone.Right => (RightTabBar, RightContentPresenter, RightZoneBorder, RightTabBarBorder, RightSplitter),
            PanelZone.Bottom => (BottomTabBar, BottomContentPresenter, BottomZoneBorder, null, BottomSplitter),
            _ => (null, null, null, null, null)
        };
    }

    /// <summary>
    /// 更新区域可见性
    /// </summary>
    private void UpdateZoneVisibility(PanelZone zone, int visiblePanelCount, bool hasActivePanel)
    {
        var hasVisiblePanels = visiblePanelCount > 0;

        switch (zone)
        {
            case PanelZone.Left:
                LeftTabBarBorder.Visibility = hasVisiblePanels ? Visibility.Visible : Visibility.Collapsed;
                if (hasActivePanel)
                {
                    LeftZoneBorder.Visibility = Visibility.Visible;
                    LeftSplitter.Visibility = Visibility.Visible;
                    LeftColumnDefinition.Width = new GridLength(_panelManager?.GetZoneSize(PanelZone.Left) ?? 280);
                    LeftColumnDefinition.MinWidth = 200;
                    LeftColumnDefinition.MaxWidth = 450;
                }
                else
                {
                    LeftZoneBorder.Visibility = Visibility.Collapsed;
                    LeftSplitter.Visibility = Visibility.Collapsed;
                    LeftColumnDefinition.Width = new GridLength(0);
                    LeftColumnDefinition.MinWidth = 0;
                    LeftColumnDefinition.MaxWidth = double.PositiveInfinity;
                }
                break;

            case PanelZone.Right:
                RightTabBarBorder.Visibility = hasVisiblePanels ? Visibility.Visible : Visibility.Collapsed;
                if (hasActivePanel)
                {
                    RightZoneBorder.Visibility = Visibility.Visible;
                    RightSplitter.Visibility = Visibility.Visible;
                    RightColumnDefinition.Width = new GridLength(_panelManager?.GetZoneSize(PanelZone.Right) ?? 300);
                    RightColumnDefinition.MinWidth = 200;
                    RightColumnDefinition.MaxWidth = 450;
                }
                else
                {
                    RightZoneBorder.Visibility = Visibility.Collapsed;
                    RightSplitter.Visibility = Visibility.Collapsed;
                    RightColumnDefinition.Width = new GridLength(0);
                    RightColumnDefinition.MinWidth = 0;
                    RightColumnDefinition.MaxWidth = double.PositiveInfinity;
                }
                break;

            case PanelZone.Bottom:
                if (hasVisiblePanels)
                {
                    BottomZoneBorder.Visibility = Visibility.Visible;
                    if (hasActivePanel)
                    {
                        BottomSplitter.Visibility = Visibility.Visible;
                        BottomRowDefinition.Height = new GridLength(_panelManager?.GetZoneSize(PanelZone.Bottom) ?? 200);
                        BottomRowDefinition.MinHeight = 100;
                    }
                    else
                    {
                        BottomSplitter.Visibility = Visibility.Collapsed;
                        BottomRowDefinition.Height = GridLength.Auto;
                        BottomRowDefinition.MinHeight = 0;
                    }
                }
                else
                {
                    BottomZoneBorder.Visibility = Visibility.Collapsed;
                    BottomSplitter.Visibility = Visibility.Collapsed;
                    BottomRowDefinition.Height = new GridLength(0);
                    BottomRowDefinition.MinHeight = 0;
                }
                break;
        }
    }

    /// <summary>
    /// 将面板浮动为独立窗口
    /// </summary>
    private void FloatPanel(string panelId, Point screenPoint)
    {
        var panelInfo = _panelManager?.GetPanel(panelId);
        if (panelInfo == null || !panelInfo.IsMovable) return;
        if (!_panelContents.TryGetValue(panelId, out var content)) return;

        _isFloatingInProgress = true;

        try
        {
            if (!_floatingWindows.TryGetValue(panelId, out var floatingWindow))
            {
                floatingWindow = new FloatingPanelWindow
                {
                    PanelId = panelId,
                    PanelTitle = panelInfo.Title
                };

                if (_ownerWindow != null)
                {
                    floatingWindow.Owner = _ownerWindow;
                }

                floatingWindow.DockRequested += (_, zone) => DockPanel(panelId, zone);
                floatingWindow.PanelHidden += (_, _) => HidePanel(panelId);
                floatingWindow.BoundsChanged += (_, bounds) =>
                {
                    _panelManager?.UpdateFloatingPanelBounds(panelId, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                };

                _floatingWindows[panelId] = floatingWindow;
            }

            // 清除内容的旧父级
            if (content is FrameworkElement fe && fe.Parent is ContentPresenter oldPresenter)
            {
                oldPresenter.Content = null;
            }

            floatingWindow.PanelContent = content;

            var width = panelInfo.FloatingWidth > 0 ? panelInfo.FloatingWidth : 300;
            var height = panelInfo.FloatingHeight > 0 ? panelInfo.FloatingHeight : 400;

            double x, y;
            if (_ownerWindow != null)
            {
                x = _ownerWindow.Left + (_ownerWindow.Width - width) / 2 + 50;
                y = _ownerWindow.Top + (_ownerWindow.Height - height) / 2;
            }
            else
            {
                x = (SystemParameters.PrimaryScreenWidth - width) / 2;
                y = (SystemParameters.PrimaryScreenHeight - height) / 2;
            }

            _panelManager?.SetPanelFloating(panelId, x, y, width, height);
            floatingWindow.ShowAt(x, y, width, height);
        }
        finally
        {
            _isFloatingInProgress = false;
        }
    }

    /// <summary>
    /// 将面板停靠回指定区域
    /// </summary>
    public void DockPanel(string panelId, PanelZone zone)
    {
        _panelManager?.DockPanel(panelId, zone);

        if (_floatingWindows.TryGetValue(panelId, out var floatingWindow))
        {
            floatingWindow.Hide();
        }
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HidePanel(string panelId)
    {
        _panelManager?.SetPanelVisibility(panelId, false);

        if (_floatingWindows.TryGetValue(panelId, out var floatingWindow))
        {
            floatingWindow.Hide();
        }

        PanelVisibilityChanged?.Invoke(this, (panelId, false));
    }

    /// <summary>
    /// 显示面板
    /// </summary>
    public void ShowPanel(string panelId)
    {
        var panelInfo = _panelManager?.GetPanel(panelId);
        if (panelInfo == null) return;

        _panelManager?.SetPanelVisibility(panelId, true);

        if (panelInfo.IsFloating && _floatingWindows.TryGetValue(panelId, out var floatingWindow))
        {
            if (_panelContents.TryGetValue(panelId, out var content))
            {
                floatingWindow.PanelContent = content;
            }
            floatingWindow.ShowAt(panelInfo.FloatingX, panelInfo.FloatingY,
                panelInfo.FloatingWidth, panelInfo.FloatingHeight);
        }
        else
        {
            // 自动展开并激活该面板
            _panelManager?.ActivatePanelInZone(panelId);
        }

        PanelVisibilityChanged?.Invoke(this, (panelId, true));
    }

    /// <summary>
    /// 切换面板可见性
    /// </summary>
    public void TogglePanelVisibility(string panelId)
    {
        var panelInfo = _panelManager?.GetPanel(panelId);
        if (panelInfo == null) return;

        if (panelInfo.IsVisible)
        {
            HidePanel(panelId);
        }
        else
        {
            ShowPanel(panelId);
        }
    }

    /// <summary>
    /// 设置所有者窗口
    /// </summary>
    public void SetOwnerWindow(Window window)
    {
        _ownerWindow = window;
    }

    /// <summary>
    /// 恢复浮动面板状态
    /// </summary>
    public void RestoreFloatingPanels()
    {
        if (_panelManager == null) return;

        foreach (var panelInfo in _panelManager.GetFloatingPanels())
        {
            if (panelInfo.IsVisible)
            {
                FloatPanel(panelInfo.Id, new Point(panelInfo.FloatingX + 50, panelInfo.FloatingY + 20));
            }
        }
    }

    /// <summary>
    /// 关闭所有浮动窗口
    /// </summary>
    public void CloseAllFloatingWindows()
    {
        foreach (var window in _floatingWindows.Values)
        {
            window.AllowClose = true;
            window.PanelContent = null;
            window.Close();
        }
        _floatingWindows.Clear();
    }

    private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_panelManager == null) return;

        // 抑制保存期间的布局刷新，避免尚未保存的区域被重置为旧值
        _isSavingLayout = true;
        try
        {
            if (LeftColumnDefinition.Width.Value > 0)
            {
                _panelManager.SetZoneSize(PanelZone.Left, LeftColumnDefinition.Width.Value);
                ZoneSizeChanged?.Invoke(this, (PanelZone.Left, LeftColumnDefinition.Width.Value));
            }

            if (RightColumnDefinition.Width.Value > 0)
            {
                _panelManager.SetZoneSize(PanelZone.Right, RightColumnDefinition.Width.Value);
                ZoneSizeChanged?.Invoke(this, (PanelZone.Right, RightColumnDefinition.Width.Value));
            }

            if (BottomRowDefinition.Height.Value > 0 && !BottomRowDefinition.Height.IsAuto)
            {
                _panelManager.SetZoneSize(PanelZone.Bottom, BottomRowDefinition.Height.Value);
                ZoneSizeChanged?.Invoke(this, (PanelZone.Bottom, BottomRowDefinition.Height.Value));
            }
        }
        finally
        {
            _isSavingLayout = false;
        }
    }
}
