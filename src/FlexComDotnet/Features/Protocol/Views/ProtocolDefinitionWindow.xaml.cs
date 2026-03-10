using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    private void ProtocolList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProtocolParserViewModel vm && vm.SelectedDefinition != null)
        {
            if (vm.IsEditing && vm.IsDirty)
            {
                var result = MessageBox.Show(
                    "当前编辑已修改，是否放弃修改并切换？",
                    "确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    vm.ForceEditDefinitionCommand.Execute(vm.SelectedDefinition);
                }
                return;
            }

            vm.DoubleClickDefinitionCommand.Execute(null);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProtocolParserViewModel vm) return;

        if (vm.IsDirty)
        {
            var result = MessageBox.Show(
                "当前编辑已修改，确定放弃修改？",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        vm.CancelEditCommand.Execute(null);
    }

    private void EditingPanel_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is ProtocolParserViewModel vm && vm.IsEditing)
        {
            vm.MarkDirty();
        }
    }
}
