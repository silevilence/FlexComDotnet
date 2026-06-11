using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Features.Protocol.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace FlexComDotnet.Features.Serial.Views;

/// <summary>
/// SerialCommunicationView.xaml 的交互逻辑
/// </summary>
public partial class SerialCommunicationView : UserControl
{
    private ScrollViewer? _scrollViewer;
    private bool _isUserScrollingUp;

    public SerialCommunicationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ReceivedDataListBox.Loaded += OnListBoxLoaded;
    }

    private void OnListBoxLoaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer = FindVisualChild<ScrollViewer>(ReceivedDataListBox);
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null) return;

        // 用户滚动到底部 → 恢复自动滚动
        if (Math.Abs(e.VerticalOffset - _scrollViewer.ScrollableHeight) < 1.0)
        {
            _isUserScrollingUp = false;
        }
        // 用户手动向上滚动（非内容增长导致的偏移变化）
        else if (e.VerticalChange < 0 && e.ExtentHeightChange == 0)
        {
            _isUserScrollingUp = true;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SerialCommunicationViewModel oldVm)
        {
            oldVm.DisplayRecords.CollectionChanged -= OnDisplayRecordsChanged;
        }
        if (e.NewValue is SerialCommunicationViewModel newVm)
        {
            newVm.DisplayRecords.CollectionChanged += OnDisplayRecordsChanged;
            // 当 DisplayRecords 属性被替换时（如 InvalidateDisplay），重新订阅
            newVm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(SerialCommunicationViewModel.DisplayRecords))
                {
                    newVm.DisplayRecords.CollectionChanged += OnDisplayRecordsChanged;
                }
            };
        }
    }

    private void OnDisplayRecordsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (_isUserScrollingUp) return;

        // 延迟到布局完成后再滚动，确保虚拟化项已生成
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ReceivedDataListBox.Items.Count > 0)
            {
                ReceivedDataListBox.ScrollIntoView(ReceivedDataListBox.Items[^1]);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 递归查找指定类型的可视化子元素
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    private void CopyReceived_Click(object sender, RoutedEventArgs e)
    {
        if (ReceivedDataListBox.SelectedItems.Count > 0)
        {
            var text = string.Join(Environment.NewLine, ReceivedDataListBox.SelectedItems.Cast<string>());
            Clipboard.SetText(text);
        }
    }

    private void SelectAllReceived_Click(object sender, RoutedEventArgs e)
    {
        ReceivedDataListBox.SelectAll();
    }

    /// <summary>
    /// 保存日志按钮点击事件
    /// </summary>
    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SerialCommunicationViewModel viewModel)
        {
            return;
        }

        var extension = viewModel.GetRecommendedLogExtension();
        var filter = extension == ".txt"
            ? "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
            : "二进制文件 (*.bin)|*.bin|所有文件 (*.*)|*.*";

        var dialog = new SaveFileDialog
        {
            Title = "保存通信日志",
            Filter = filter,
            DefaultExt = extension,
            FileName = $"SerialLog_{DateTime.Now:yyyyMMdd_HHmmss}{extension}"
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.SaveLog(dialog.FileName);
        }
    }

    /// <summary>
    /// 协议解析右键菜单点击事件 - 打开非模态解析窗口
    /// </summary>
    private void ProtocolParse_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SerialCommunicationViewModel viewModel)
            return;

        // 优先使用选中项的原始字节数据
        var hexData = string.Empty;
        if (ReceivedDataListBox.SelectedIndex >= 0)
        {
            var record = viewModel.GetDataRecord(ReceivedDataListBox.SelectedIndex);
            if (record != null)
            {
                hexData = HexHelper.BytesToHexString(record.Data);
            }
        }

        var parserService = App.Services.GetRequiredService<IProtocolParserService>();
        var window = new RxProtocolParseWindow(parserService, hexData)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show(); // 非模态
    }

    /// <summary>
    /// 协议组帧按钮点击事件 - 打开非模态组帧窗口
    /// </summary>
    private void ProtocolBuild_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SerialCommunicationViewModel viewModel)
            return;

        var parserService = App.Services.GetRequiredService<IProtocolParserService>();
        var window = new TxProtocolBuildWindow(parserService, viewModel)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show(); // 非模态
    }
}
