using System.Windows;
using FlexComDotnet.Core.Features.Protocol.Models;

namespace FlexComDotnet.Features.Protocol.Views;

public partial class ProtocolSaveDependencyDialog : Window
{
    public ProtocolSaveAction SelectedAction { get; private set; } = ProtocolSaveAction.Cancel;

    public ProtocolSaveDependencyDialog(string protocolName, List<string> referencingScripts)
    {
        InitializeComponent();
        var scriptList = string.Join("、", referencingScripts);
        ReferenceInfo.Text = $"协议 \"{protocolName}\" 被以下脚本引用：{scriptList}";
    }

    private void ForceSave_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = ProtocolSaveAction.ForceSave;
        DialogResult = true;
        Close();
    }

    private void CloneAsNew_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = ProtocolSaveAction.CloneAsNew;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = ProtocolSaveAction.Cancel;
        DialogResult = false;
        Close();
    }
}
