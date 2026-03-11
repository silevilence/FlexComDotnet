using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlexComDotnet.Features.EmojiSupport.Controls;

/// <summary>
/// 支持彩色 Emoji 渲染的 TextBox 控件。
/// 透明 TextBox 叠加在 Emoji.Wpf.TextBlock 之上，
/// 实现编辑态和展示态都显示彩色 Emoji。
/// </summary>
public partial class EmojiTextBox : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(EmojiTextBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 文本内容（双向绑定）
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public EmojiTextBox()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 选中所有文本
    /// </summary>
    public void SelectAll()
    {
        EditBox.Focus();
        EditBox.SelectAll();
    }

    /// <summary>
    /// 聚焦并进入编辑模式
    /// </summary>
    public new void Focus()
    {
        EditBox.Focus();
        EditBox.CaretIndex = EditBox.Text?.Length ?? 0;
    }

    private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        EditBox.Focus();
        e.Handled = true;
    }
}
