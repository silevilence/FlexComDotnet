using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlexComDotnet.Core.Features.EmojiSupport.Models;

namespace FlexComDotnet.Features.EmojiSupport.Controls;

/// <summary>
/// Emoji 补全弹出控件
/// </summary>
public partial class EmojiCompletionPopup : UserControl
{
    /// <summary>
    /// 用户选择了一个 Emoji 条目时触发
    /// </summary>
    public event EventHandler<EmojiEntry>? EmojiSelected;

    /// <summary>
    /// 弹出窗口是否打开
    /// </summary>
    public bool IsOpen
    {
        get => CompletionPopup.IsOpen;
        set => CompletionPopup.IsOpen = value;
    }

    /// <summary>
    /// 弹出窗口的放置目标
    /// </summary>
    public UIElement? PlacementTarget
    {
        get => CompletionPopup.PlacementTarget;
        set => CompletionPopup.PlacementTarget = value;
    }

    /// <summary>
    /// 水平偏移
    /// </summary>
    public double HorizontalOffset
    {
        get => CompletionPopup.HorizontalOffset;
        set => CompletionPopup.HorizontalOffset = value;
    }

    /// <summary>
    /// 垂直偏移
    /// </summary>
    public double VerticalOffset
    {
        get => CompletionPopup.VerticalOffset;
        set => CompletionPopup.VerticalOffset = value;
    }

    public EmojiCompletionPopup()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 更新候选项列表
    /// </summary>
    public void UpdateItems(IReadOnlyList<EmojiEntry> items)
    {
        CompletionListBox.ItemsSource = items;
        if (items.Count > 0)
        {
            CompletionListBox.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// 选中上一项
    /// </summary>
    public void MoveUp()
    {
        if (CompletionListBox.SelectedIndex > 0)
        {
            CompletionListBox.SelectedIndex--;
            CompletionListBox.ScrollIntoView(CompletionListBox.SelectedItem);
        }
    }

    /// <summary>
    /// 选中下一项
    /// </summary>
    public void MoveDown()
    {
        if (CompletionListBox.SelectedIndex < CompletionListBox.Items.Count - 1)
        {
            CompletionListBox.SelectedIndex++;
            CompletionListBox.ScrollIntoView(CompletionListBox.SelectedItem);
        }
    }

    /// <summary>
    /// 确认选择当前选中项
    /// </summary>
    public bool ConfirmSelection()
    {
        if (CompletionListBox.SelectedItem is EmojiEntry entry)
        {
            EmojiSelected?.Invoke(this, entry);
            IsOpen = false;
            return true;
        }
        return false;
    }

    private void CompletionListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsOpen = false;
            e.Handled = true;
        }
    }

    private void CompletionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }
}
