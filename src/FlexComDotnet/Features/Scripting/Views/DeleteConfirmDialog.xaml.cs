using System.Windows;

namespace FlexComDotnet.Features.Scripting.Views;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog(string scriptName)
    {
        InitializeComponent();
        MessageText.Text = $"确定要删除脚本 \"{scriptName}\" 吗？";
    }

    private void OK_Click(object sender, RoutedEventArgs e)
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
