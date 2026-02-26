using System.Windows;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>
/// 重命名对话框
/// </summary>
public partial class RenameDialog : Window
{
    public string NewName => NameTextBox.Text;

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameTextBox.Text = currentName;
        NameTextBox.SelectAll();
        NameTextBox.Focus();
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
