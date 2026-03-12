using System.Windows;

namespace FlexComDotnet.Features.Protocol.Views;

public partial class ProtocolDeleteDependencyDialog : Window
{
    public ProtocolDeleteDependencyDialog(string protocolName, List<string> referencingScripts)
    {
        InitializeComponent();
        var scriptList = string.Join("、", referencingScripts);
        ReferenceInfo.Text = $"协议 \"{protocolName}\" 被以下脚本引用：{scriptList}";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
