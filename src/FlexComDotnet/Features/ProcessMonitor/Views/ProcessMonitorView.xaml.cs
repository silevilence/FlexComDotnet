using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using FlexComDotnet.Core.Features.ProcessMonitor.ViewModels;

namespace FlexComDotnet.Features.ProcessMonitor.Views;

public partial class ProcessMonitorView : UserControl
{
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private ProcessMonitorViewModel? ViewModel => DataContext as ProcessMonitorViewModel;

    public ProcessMonitorView()
    {
        InitializeComponent();
        _refresh.Tick += (_, _) => ViewModel?.Refresh();
        Loaded += (_, _) => { ViewModel?.Refresh(); _refresh.Start(); };
        Unloaded += (_, _) => _refresh.Stop();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "可执行程序 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true && ViewModel is { } vm) vm.ExecutablePath = dialog.FileName;
    }

    private void ExportRecords_Click(object sender, RoutedEventArgs e) => Export(false);
    private void ExportFrames_Click(object sender, RoutedEventArgs e) => Export(true);

    private async void Export(bool frames)
    {
        if (ViewModel is not { } vm) return;
        var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", DefaultExt = ".json",
            FileName = frames ? "process-monitor-frames.json" : "process-monitor-records.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            vm.Refresh();
            await File.WriteAllTextAsync(dialog.FileName, frames ? vm.ExportFramesJson() : vm.ExportVisibleJson());
            vm.OperationStatus = "已导出：" + dialog.FileName;
        }
        catch (Exception error) { vm.OperationStatus = "导出失败：" + error.Message; }
    }
}
