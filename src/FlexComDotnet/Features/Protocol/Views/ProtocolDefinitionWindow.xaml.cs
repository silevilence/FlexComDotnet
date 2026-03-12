using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlexComDotnet.Core.Features.Protocol.Models;
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

        // 注册依赖拦截事件
        viewModel.SaveInterceptRequested += OnSaveInterceptRequested;
        viewModel.DeleteInterceptRequested += OnDeleteInterceptRequested;
    }

    private Task<ProtocolSaveAction> OnSaveInterceptRequested(string protocolName, List<string> referencingScripts)
    {
        var scriptList = string.Join("\n  • ", referencingScripts);
        var result = MessageBox.Show(
            $"协议 \"{protocolName}\" 被以下脚本引用：\n  • {scriptList}\n\n修改此协议可能影响这些脚本的运行。\n\n• 选择「是」强行覆盖保存\n• 选择「否」另存为新协议（克隆模式）\n• 选择「取消」放弃保存",
            "依赖检查",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return Task.FromResult(result switch
        {
            MessageBoxResult.Yes => ProtocolSaveAction.ForceSave,
            MessageBoxResult.No => ProtocolSaveAction.CloneAsNew,
            _ => ProtocolSaveAction.Cancel
        });
    }

    private Task<bool> OnDeleteInterceptRequested(string protocolName, List<string> referencingScripts)
    {
        var scriptList = string.Join("\n  • ", referencingScripts);
        var result = MessageBox.Show(
            $"协议 \"{protocolName}\" 被以下脚本引用：\n  • {scriptList}\n\n删除此协议将导致这些脚本无法正常工作。\n确定要删除吗？",
            "依赖检查",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return Task.FromResult(result == MessageBoxResult.Yes);
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
