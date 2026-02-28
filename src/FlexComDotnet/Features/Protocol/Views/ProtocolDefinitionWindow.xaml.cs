using System.Windows;
using FlexComDotnet.Core.Features.Protocol.ViewModels;

namespace FlexComDotnet.Features.Protocol.Views;

/// <summary>
/// 协议定义编辑器窗口
/// </summary>
public partial class ProtocolDefinitionWindow : Window
{
    public ProtocolDefinitionWindow(ProtocolParserViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
