using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>
/// 多区域布局控件
/// </summary>
public partial class MultiZoneLayout : UserControl
{
    private readonly Dictionary<string, CollapsiblePanel> _panelControls = [];
    private readonly Dictionary<string, FloatingPanelWindow> _floatingWindows = [];
    private readonly Dictionary<string, UIElement> _panelContents = [];
    private IPanelManager? _panelManager;
    private Window? _ownerWindow;
    private bool _isFloatingInProgress;

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

        // 初始化区域尺寸
        var state = _panelManager.GetLayoutState();
        SetLeftZoneWidth(state.LeftZoneWidth);
        SetRightZoneWidth(state.RightZoneWidth);
        SetBottomZoneHeight(state.BottomZoneHeight);

        // 初始化区域折叠状态
        SetZoneCollapsed(PanelZone.Left, state.IsLeftZoneCollapsed);
        SetZoneCollapsed(PanelZone.Right, state.IsRightZoneCollapsed);
        SetZoneCollapsed(PanelZone.Bottom, state.IsBottomZoneCollapsed);

        RefreshLayout();
    }

    /// <summary>
    /// 添加面板
    /// </summary>
    public void AddPanel(string panelId, string title, UIElement content, PanelZone zone, bool isMovable = true, int order = 0)
    {
        // 保存内容引用
        _panelContents[panelId] = content;

        // 创建可折叠面板
        var collapsiblePanel = new CollapsiblePanel
        {
            PanelId = panelId,
            Title = title,
            PanelContent = content,
            IsMovable = isMovable,
            CurrentZone = zone
        };

        // 订阅事件
        collapsiblePanel.ExpandedChanged += (sender, isExpanded) =>
        {
            _panelManager?.SetPanelExpanded(panelId, isExpanded);
        };

        collapsiblePanel.MoveRequested += (sender, targetZone) =>
        {
            MovePanel(panelId, targetZone);
        };

        collapsiblePanel.FloatRequested += (sender, screenPoint) =>
        {
            FloatPanel(panelId, screenPoint);
        };

        collapsiblePanel.HideRequested += (sender, _) =>
        {
            HidePanel(panelId);
        };

        _panelControls[panelId] = collapsiblePanel;

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
        if (_panelManager != null && _panelManager.MovePanel(panelId, targetZone))
        {
            if (_panelControls.TryGetValue(panelId, out var panel))
            {
                panel.CurrentZone = targetZone;
            }
        }
    }

    /// <summary>
    /// 设置区域折叠状态
    /// </summary>
    public void SetZoneCollapsed(PanelZone zone, bool isCollapsed)
    {
        switch (zone)
        {
            case PanelZone.Left:
                if (isCollapsed)
                {
                    LeftZoneBorder.Visibility = Visibility.Collapsed;
                    LeftSplitter.Visibility = Visibility.Collapsed;
                    LeftColumnDefinition.Width = new GridLength(0);
                    LeftColumnDefinition.MinWidth = 0;
                }
                else
                {
                    LeftZoneBorder.Visibility = Visibility.Visible;
                    LeftSplitter.Visibility = Visibility.Visible;
                    LeftColumnDefinition.Width = new GridLength(_panelManager?.GetZoneSize(PanelZone.Left) ?? 280);
                    LeftColumnDefinition.MinWidth = 200;
                }
                break;

            case PanelZone.Right:
                if (isCollapsed)
                {
                    RightZoneBorder.Visibility = Visibility.Collapsed;
                    RightSplitter.Visibility = Visibility.Collapsed;
                    RightColumnDefinition.Width = new GridLength(0);
                    RightColumnDefinition.MinWidth = 0;
                }
                else
                {
                    RightZoneBorder.Visibility = Visibility.Visible;
                    RightSplitter.Visibility = Visibility.Visible;
                    RightColumnDefinition.Width = new GridLength(_panelManager?.GetZoneSize(PanelZone.Right) ?? 300);
                    RightColumnDefinition.MinWidth = 200;
                }
                break;

            case PanelZone.Bottom:
                if (isCollapsed)
                {
                    BottomZoneBorder.Visibility = Visibility.Collapsed;
                    BottomSplitter.Visibility = Visibility.Collapsed;
                    BottomRowDefinition.Height = new GridLength(0);
                    BottomRowDefinition.MinHeight = 0;
                }
                else
                {
                    BottomZoneBorder.Visibility = Visibility.Visible;
                    BottomSplitter.Visibility = Visibility.Visible;
                    BottomRowDefinition.Height = new GridLength(_panelManager?.GetZoneSize(PanelZone.Bottom) ?? 200);
                    BottomRowDefinition.MinHeight = 100;
                }
                break;
        }

        _panelManager?.SetZoneCollapsed(zone, isCollapsed);
    }

    /// <summary>
    /// 获取区域是否折叠
    /// </summary>
    public bool IsZoneCollapsed(PanelZone zone)
    {
        return zone switch
        {
            PanelZone.Left => LeftZoneBorder.Visibility != Visibility.Visible,
            PanelZone.Right => RightZoneBorder.Visibility != Visibility.Visible,
            PanelZone.Bottom => BottomZoneBorder.Visibility != Visibility.Visible,
            _ => false
        };
    }

    private void SetLeftZoneWidth(double width)
    {
        LeftColumnDefinition.Width = new GridLength(width);
    }

    private void SetRightZoneWidth(double width)
    {
        RightColumnDefinition.Width = new GridLength(width);
    }

    private void SetBottomZoneHeight(double height)
    {
        BottomRowDefinition.Height = new GridLength(height);
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        // 如果正在处理浮动操作，跳过刷新
        if (_isFloatingInProgress) return;
        
        // 在 UI 线程上执行
        Dispatcher.Invoke(RefreshLayout);
    }

    private void RefreshLayout()
    {
        if (_panelManager == null) return;

        // 清空所有区域
        LeftZonePanel.Children.Clear();
        RightZonePanel.Children.Clear();
        BottomZonePanel.Children.Clear();

        // 按区域添加面板（排除浮动面板）
        foreach (var zone in new[] { PanelZone.Left, PanelZone.Right, PanelZone.Bottom })
        {
            var panels = _panelManager.GetPanelsInZone(zone);
            var targetPanel = zone switch
            {
                PanelZone.Left => LeftZonePanel,
                PanelZone.Right => RightZonePanel,
                PanelZone.Bottom => BottomZonePanel,
                _ => null
            };

            if (targetPanel == null) continue;

            foreach (var panelInfo in panels)
            {
                // 跳过浮动面板
                if (panelInfo.IsFloating) continue;

                if (_panelControls.TryGetValue(panelInfo.Id, out var control))
                {
                    // 更新面板状态
                    control.IsExpanded = panelInfo.IsExpanded;
                    control.CurrentZone = panelInfo.Zone;
                    control.Visibility = panelInfo.IsVisible ? Visibility.Visible : Visibility.Collapsed;

                    // 确保内容在 CollapsiblePanel 中
                    if (_panelContents.TryGetValue(panelInfo.Id, out var content))
                    {
                        // 从浮动窗口移除内容（如果有）
                        if (_floatingWindows.TryGetValue(panelInfo.Id, out var floatingWindow))
                        {
                            floatingWindow.PanelContent = null;
                            floatingWindow.Hide();
                        }
                        
                        // 确保内容不在其他容器中
                        if (content is FrameworkElement fe && fe.Parent is ContentPresenter oldPresenter)
                        {
                            oldPresenter.Content = null;
                        }
                        
                        control.PanelContent = content;
                    }

                    // 从旧父容器移除
                    if (control.Parent is Panel oldParent)
                    {
                        oldParent.Children.Remove(control);
                    }

                    // 添加到新区域
                    targetPanel.Children.Add(control);
                }
            }
        }
    }

    /// <summary>
    /// 将面板浮动为独立窗口
    /// </summary>
    private void FloatPanel(string panelId, Point screenPoint)
    {
        var panelInfo = _panelManager?.GetPanel(panelId);
        if (panelInfo == null || !panelInfo.IsMovable)
        {
            return;
        }

        if (!_panelContents.TryGetValue(panelId, out var content))
        {
            return;
        }

        // 标记正在进行浮动操作，避免触发刷新
        _isFloatingInProgress = true;
        
        try
        {
            // 获取或创建浮动窗口
            if (!_floatingWindows.TryGetValue(panelId, out var floatingWindow))
            {
                floatingWindow = new FloatingPanelWindow
                {
                    PanelId = panelId,
                    PanelTitle = panelInfo.Title
                };

                // 设置 Owner（如果可用）
                if (_ownerWindow != null)
                {
                    floatingWindow.Owner = _ownerWindow;
                }

                floatingWindow.DockRequested += (sender, zone) =>
                {
                    DockPanel(panelId, zone);
                };

                floatingWindow.PanelHidden += (sender, _) =>
                {
                    HidePanel(panelId);
                };

                floatingWindow.BoundsChanged += (sender, bounds) =>
                {
                    _panelManager?.UpdateFloatingPanelBounds(panelId, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                };

                _floatingWindows[panelId] = floatingWindow;
            }

            // 从 CollapsiblePanel 移除内容
            if (_panelControls.TryGetValue(panelId, out var collapsiblePanel))
            {
                collapsiblePanel.ClearValue(CollapsiblePanel.PanelContentProperty);
            }

            // 确保内容从旧父级移除
            if (content is FrameworkElement fe && fe.Parent is ContentPresenter oldPresenter)
            {
                oldPresenter.Content = null;
            }

            // 设置浮动窗口内容
            floatingWindow.PanelContent = content;

            // 计算显示位置：使用主窗口位置作为参考
            var width = panelInfo.FloatingWidth > 0 ? panelInfo.FloatingWidth : 300;
            var height = panelInfo.FloatingHeight > 0 ? panelInfo.FloatingHeight : 400;
            
            double x, y;
            if (_ownerWindow != null)
            {
                // 在主窗口中央偏右显示
                x = _ownerWindow.Left + (_ownerWindow.Width - width) / 2 + 50;
                y = _ownerWindow.Top + (_ownerWindow.Height - height) / 2;
            }
            else
            {
                // 使用屏幕中央
                x = (SystemParameters.PrimaryScreenWidth - width) / 2;
                y = (SystemParameters.PrimaryScreenHeight - height) / 2;
            }

            // 更新面板管理器状态
            _panelManager?.SetPanelFloating(panelId, x, y, width, height);

            // 显示浮动窗口
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
        
        // 隐藏浮动窗口
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
        
        // 隐藏浮动窗口（如果有）
        if (_floatingWindows.TryGetValue(panelId, out var floatingWindow))
        {
            floatingWindow.Hide();
        }

        // 触发面板可见性变更事件
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

        // 如果之前是浮动状态，恢复浮动窗口
        if (panelInfo.IsFloating && _floatingWindows.TryGetValue(panelId, out var floatingWindow))
        {
            if (_panelContents.TryGetValue(panelId, out var content))
            {
                floatingWindow.PanelContent = content;
            }
            floatingWindow.ShowAt(panelInfo.FloatingX, panelInfo.FloatingY, 
                panelInfo.FloatingWidth, panelInfo.FloatingHeight);
        }

        // 触发面板可见性变更事件
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
            window.AllowClose = true;  // 允许关闭，不触发隐藏逻辑
            window.PanelContent = null;
            window.Close();
        }
        _floatingWindows.Clear();
    }

    /// <summary>
    /// 面板可见性变更事件
    /// </summary>
    public event EventHandler<(string PanelId, bool IsVisible)>? PanelVisibilityChanged;

    private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        // 保存新的区域尺寸
        if (_panelManager != null)
        {
            _panelManager.SetZoneSize(PanelZone.Left, LeftColumnDefinition.Width.Value);
            _panelManager.SetZoneSize(PanelZone.Right, RightColumnDefinition.Width.Value);
            _panelManager.SetZoneSize(PanelZone.Bottom, BottomRowDefinition.Height.Value);

            ZoneSizeChanged?.Invoke(this, (PanelZone.Left, LeftColumnDefinition.Width.Value));
            ZoneSizeChanged?.Invoke(this, (PanelZone.Right, RightColumnDefinition.Width.Value));
            ZoneSizeChanged?.Invoke(this, (PanelZone.Bottom, BottomRowDefinition.Height.Value));
        }
    }
}
