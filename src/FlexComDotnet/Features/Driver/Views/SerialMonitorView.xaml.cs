using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void ViewRawRecord_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is SerialMonitorRecordViewModel;
        e.Handled = true;
    }

    private void ViewRawRecord_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        // A row context menu uses its placement target, not the previously selected row.
        if (e.Parameter is SerialMonitorRecordViewModel record)
        {
            new SerialMonitorRawRecordWindow(record) { Owner = Window.GetWindow(this) }.ShowDialog();
            e.Handled = true;
        }
    }
}
