using System.Windows;
using System.Windows.Input;
using FlexComDotnet.Core.Features.Layout.Models;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>
/// 浮动面板窗口
/// </summary>
public partial class FloatingPanelWindow : Window
{
    /// <summary>
    /// 面板 ID
    /// </summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>
    /// 面板标题
    /// </summary>
    public string PanelTitle
    {
        get => TitleText.Text;
        set
        {
            TitleText.Text = value;
            Title = value;
        }
    }

    /// <summary>
    /// 面板内容
    /// </summary>
    public object? PanelContent
    {
        get => ContentArea.Content;
        set => ContentArea.Content = value;
    }

    /// <summary>
    /// 请求停靠事件
    /// </summary>
    public event EventHandler<PanelZone>? DockRequested;

    /// <summary>
    /// 窗口关闭事件（隐藏面板）
    /// </summary>
    public event EventHandler? PanelHidden;

    /// <summary>
    /// 是否允许关闭（应用退出时设置为 true）
    /// </summary>
    public bool AllowClose { get; set; }

    /// <summary>
    /// 位置或尺寸变更事件
    /// </summary>
    public event EventHandler<(double X, double Y, double Width, double Height)>? BoundsChanged;

    public FloatingPanelWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击标题栏停靠到默认区域（右侧）
            DockRequested?.Invoke(this, PanelZone.Right);
        }
        else
        {
            // 拖动窗口
            DragMove();
        }
    }

    private void DockLeft_Click(object sender, RoutedEventArgs e)
    {
        DockRequested?.Invoke(this, PanelZone.Left);
    }

    private void DockRight_Click(object sender, RoutedEventArgs e)
    {
        DockRequested?.Invoke(this, PanelZone.Right);
    }

    private void DockBottom_Click(object sender, RoutedEventArgs e)
    {
        DockRequested?.Invoke(this, PanelZone.Bottom);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // 如果允许关闭（应用退出时），直接关闭
        if (AllowClose)
        {
            return;
        }
        
        // 否则取消关闭，改为隐藏
        e.Cancel = true;
        Hide();
        PanelHidden?.Invoke(this, EventArgs.Empty);
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        NotifyBoundsChanged();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyBoundsChanged();
    }

    private void NotifyBoundsChanged()
    {
        BoundsChanged?.Invoke(this, (Left, Top, Width, Height));
    }

    private void Content_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("PanelId"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Content_Drop(object sender, DragEventArgs e)
    {
        // 处理拖入的面板（暂不实现合并功能）
        e.Handled = true;
    }

    /// <summary>
    /// 显示窗口并设置位置
    /// </summary>
    public void ShowAt(double x, double y, double width, double height)
    {
        Left = x;
        Top = y;
        Width = width;
        Height = height;
        Show();
        Activate();
    }
}
