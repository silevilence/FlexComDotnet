using System.Windows;
using System.Windows.Controls;
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
    public SerialCommunicationView()
    {
        InitializeComponent();
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
