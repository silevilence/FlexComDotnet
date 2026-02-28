using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    private void ListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is ListBox listBox && listBox.ContextMenu != null)
        {
            listBox.ContextMenu.Placement = PlacementMode.MousePoint;
            listBox.ContextMenu.HorizontalOffset = 0;
        }
    }
}
