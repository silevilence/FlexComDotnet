using System.Windows.Controls;
using FlexComDotnet.Core.Features.AutoReply.ViewModels;

namespace FlexComDotnet.Features.AutoReply.Views;

/// <summary>
/// AutoReplyView.xaml 的交互逻辑
/// </summary>
public partial class AutoReplyView : UserControl
{
    public AutoReplyView(AutoReplyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
