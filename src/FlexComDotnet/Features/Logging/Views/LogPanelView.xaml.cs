using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.ViewModels;

namespace FlexComDotnet.Features.Logging.Views;

/// <summary>
/// LogPanelView.xaml 的交互逻辑
/// </summary>
public partial class LogPanelView : UserControl
{
    private readonly LogPanelViewModel _viewModel;

    public LogPanelView(LogPanelViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 注入 WPF Dispatcher 调度器
        viewModel.DispatcherAction = action => Dispatcher.Invoke(action);

        // 日志自动滚动到底部
        if (viewModel.FilteredEntries is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (_, _) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                    }
                });
            };
        }
    }

    #region 右键菜单

    private void CopyLogMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (LogListBox.SelectedItems.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var item in LogListBox.SelectedItems)
        {
            if (item is LogEntry entry)
            {
                var sourceDisplay = entry.Source switch
                {
                    LogSource.System => "系统",
                    LogSource.Serial => "串口",
                    LogSource.Network => "网络",
                    LogSource.Script => "脚本",
                    LogSource.AutoReply => "自动回复",
                    LogSource.Protocol => "协议",
                    LogSource.Visualization => "可视化",
                    _ => entry.Source.ToString()
                };
                sb.AppendLine($"{entry.Timestamp:HH:mm:ss.fff} [{entry.Level}] [{sourceDisplay}] {entry.Message}");
            }
        }
        if (sb.Length > 0)
        {
            Clipboard.SetText(sb.ToString().TrimEnd());
        }
    }

    private void SelectAllLogMenuItem_Click(object sender, RoutedEventArgs e)
    {
        LogListBox.SelectAll();
    }

    #endregion

}
