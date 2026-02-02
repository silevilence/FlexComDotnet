using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlexComDotnet.Core.Features.Serial.ViewModels;

namespace FlexComDotnet.Features.Serial.Views;

/// <summary>
/// CommandListView.xaml 的交互逻辑
/// </summary>
public partial class CommandListView : UserControl
{
    public CommandListView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 发送按钮点击事件
    /// </summary>
    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CommandItemViewModel command)
        {
            if (DataContext is CommandListViewModel viewModel)
            {
                viewModel.SendCommandByDoubleClick(command);
            }
        }
    }

    /// <summary>
    /// 列表项双击事件
    /// </summary>
    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is CommandItemViewModel command)
        {
            if (DataContext is CommandListViewModel viewModel)
            {
                viewModel.SendCommandByDoubleClick(command);
            }
        }
    }
}
