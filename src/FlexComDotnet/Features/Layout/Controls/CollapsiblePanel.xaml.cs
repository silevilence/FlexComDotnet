using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FlexComDotnet.Core.Features.Layout.Models;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>
/// 可折叠面板控件
/// </summary>
public partial class CollapsiblePanel : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// 面板标题依赖属性
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(CollapsiblePanel),
            new PropertyMetadata("面板标题", OnTitleChanged));

    /// <summary>
    /// 面板标题
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// 面板ID依赖属性
    /// </summary>
    public static readonly DependencyProperty PanelIdProperty =
        DependencyProperty.Register(
            nameof(PanelId),
            typeof(string),
            typeof(CollapsiblePanel),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 面板唯一标识符
    /// </summary>
    public string PanelId
    {
        get => (string)GetValue(PanelIdProperty);
        set => SetValue(PanelIdProperty, value);
    }

    /// <summary>
    /// 是否展开依赖属性
    /// </summary>
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(CollapsiblePanel),
            new PropertyMetadata(true, OnIsExpandedChanged));

    /// <summary>
    /// 面板是否展开
    /// </summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// 是否可移动依赖属性
    /// </summary>
    public static readonly DependencyProperty IsMovableProperty =
        DependencyProperty.Register(
            nameof(IsMovable),
            typeof(bool),
            typeof(CollapsiblePanel),
            new PropertyMetadata(true));

    /// <summary>
    /// 面板是否可移动
    /// </summary>
    public bool IsMovable
    {
        get => (bool)GetValue(IsMovableProperty);
        set => SetValue(IsMovableProperty, value);
    }

    /// <summary>
    /// 面板内容依赖属性
    /// </summary>
    public static readonly DependencyProperty PanelContentProperty =
        DependencyProperty.Register(
            nameof(PanelContent),
            typeof(object),
            typeof(CollapsiblePanel),
            new PropertyMetadata(null, OnPanelContentChanged));

    /// <summary>
    /// 面板内容
    /// </summary>
    public object PanelContent
    {
        get => GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }

    /// <summary>
    /// 当前所在区域依赖属性
    /// </summary>
    public static readonly DependencyProperty CurrentZoneProperty =
        DependencyProperty.Register(
            nameof(CurrentZone),
            typeof(PanelZone),
            typeof(CollapsiblePanel),
            new PropertyMetadata(PanelZone.Left));

    /// <summary>
    /// 当前所在区域
    /// </summary>
    public PanelZone CurrentZone
    {
        get => (PanelZone)GetValue(CurrentZoneProperty);
        set => SetValue(CurrentZoneProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// 展开/折叠状态变更事件
    /// </summary>
    public event EventHandler<bool>? ExpandedChanged;

    /// <summary>
    /// 请求移动到其他区域事件
    /// </summary>
    public event EventHandler<PanelZone>? MoveRequested;

    /// <summary>
    /// 请求浮动（脱离 Dock）事件
    /// </summary>
    public event EventHandler<Point>? FloatRequested;

    /// <summary>
    /// 请求隐藏面板事件
    /// </summary>
    public event EventHandler? HideRequested;

    #endregion

    public CollapsiblePanel()
    {
        InitializeComponent();
        UpdateVisualState();
        SetupContextMenu();
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollapsiblePanel panel)
        {
            panel.TitleText.Text = e.NewValue?.ToString() ?? string.Empty;
        }
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollapsiblePanel panel)
        {
            panel.UpdateVisualState();
            panel.ExpandedChanged?.Invoke(panel, (bool)e.NewValue);
        }
    }

    private static void OnPanelContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollapsiblePanel panel)
        {
            panel.ContentArea.Content = e.NewValue;
        }
    }

    private void UpdateVisualState()
    {
        // 更新内容区域可见性
        ContentBorder.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;

        // 更新折叠图标旋转角度
        var targetAngle = IsExpanded ? 0 : -90;
        var animation = new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        ToggleIconRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetupContextMenu()
    {
        var contextMenu = new ContextMenu();

        // 移动到左侧选项
        var moveToLeftItem = new MenuItem { Header = "移动到左侧" };
        moveToLeftItem.Click += (_, _) => RequestMove(PanelZone.Left);
        contextMenu.Items.Add(moveToLeftItem);

        // 移动到右侧选项
        var moveToRightItem = new MenuItem { Header = "移动到右侧" };
        moveToRightItem.Click += (_, _) => RequestMove(PanelZone.Right);
        contextMenu.Items.Add(moveToRightItem);

        // 移动到底部选项
        var moveToBottomItem = new MenuItem { Header = "移动到底部" };
        moveToBottomItem.Click += (_, _) => RequestMove(PanelZone.Bottom);
        contextMenu.Items.Add(moveToBottomItem);

        contextMenu.Items.Add(new Separator());

        // 弹出为独立窗口
        var floatItem = new MenuItem { Header = "弹出为独立窗口" };
        floatItem.Click += (_, _) =>
        {
            var screenPoint = PointToScreen(new Point(0, 0));
            FloatRequested?.Invoke(this, screenPoint);
        };
        contextMenu.Items.Add(floatItem);

        contextMenu.Items.Add(new Separator());

        // 隐藏面板
        var hideItem = new MenuItem { Header = "隐藏面板" };
        hideItem.Click += (_, _) => HideRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(hideItem);

        // 更新菜单项状态
        contextMenu.Opened += (_, _) =>
        {
            // 禁用不可移动面板的移动选项
            moveToLeftItem.IsEnabled = IsMovable && CurrentZone != PanelZone.Left;
            moveToRightItem.IsEnabled = IsMovable && CurrentZone != PanelZone.Right;
            moveToBottomItem.IsEnabled = IsMovable && CurrentZone != PanelZone.Bottom;
            floatItem.IsEnabled = IsMovable;
        };

        HeaderBorder.ContextMenu = contextMenu;
    }

    private void RequestMove(PanelZone targetZone)
    {
        if (IsMovable && targetZone != CurrentZone)
        {
            MoveRequested?.Invoke(this, targetZone);
        }
    }
}
