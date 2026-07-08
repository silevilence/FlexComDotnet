using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Core.Features.Driver.ViewModels;
using Microsoft.Win32;

namespace FlexComDotnet.Features.Driver.Views;

public partial class SerialMonitorView : UserControl
{
    private readonly SerialMonitorViewModel _viewModel;

    public SerialMonitorView(SerialMonitorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.DispatcherAction = action => Dispatcher.Invoke(action);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存监控数据",
            Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"serial-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ExportRecords(dialog.FileName);
        }
    }
}
