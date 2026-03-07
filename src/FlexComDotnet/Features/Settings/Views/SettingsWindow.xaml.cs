using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Core.Features.Settings.ViewModels;

namespace FlexComDotnet.Features.Settings.Views;

/// <summary>
/// 设置窗口
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    /// 面板可见性切换事件（转发给 MainWindow 处理）
    /// </summary>
    public event EventHandler<string>? PanelVisibilityToggled;

    private void PanelCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string panelId)
        {
            PanelVisibilityToggled?.Invoke(this, panelId);
            // 刷新面板列表
            PanelList.ItemsSource = null;
            PanelList.ItemsSource = _viewModel.PanelItems;
        }
    }

    private void OpenLogDirectory_Click(object sender, RoutedEventArgs e)
    {
        var logDir = _viewModel.LogDirectory;
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = logDir,
            UseShellExecute = true
        });
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/silevilence/FlexComDotnet",
            UseShellExecute = true
        });
    }
}
