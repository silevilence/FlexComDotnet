using System.Windows;
using FlexComDotnet.Core.Features.Checksum.ViewModels;

namespace FlexComDotnet.Features.Checksum.Views;

/// <summary>
/// 校验和计算器窗口
/// </summary>
public partial class ChecksumCalculatorWindow : Window
{
    private readonly ChecksumCalculatorViewModel _viewModel;

    /// <summary>
    /// 请求获取发送帧数据的回调 (返回数据字符串和是否为Hex模式)
    /// </summary>
    public Func<(string? Data, bool IsHexMode)>? GetSendFrameData { get; set; }

    /// <summary>
    /// 请求附加数据到发送帧的回调
    /// </summary>
    public Action<byte[]>? AppendToSendFrame { get; set; }

    public ChecksumCalculatorWindow(ChecksumCalculatorViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        DataContext = _viewModel;

        // 绑定事件
        _viewModel.CopyToClipboardRequested += OnCopyToClipboard;
        _viewModel.PasteFromClipboardRequested += OnPasteFromClipboard;
        _viewModel.AppendToSendFrameRequested += OnAppendToSendFrame;
    }

    private void OnCopyToClipboard(object? sender, string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // 忽略剪贴板错误
        }
    }

    private void OnPasteFromClipboard(object? sender, Action<string?> callback)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                callback(Clipboard.GetText());
            }
        }
        catch
        {
            // 忽略剪贴板错误
        }
    }

    private void OnAppendToSendFrame(object? sender, byte[] data)
    {
        AppendToSendFrame?.Invoke(data);
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var result = GetSendFrameData?.Invoke();
        if (result.HasValue && !string.IsNullOrEmpty(result.Value.Data))
        {
            _viewModel.ImportFromSendFrame(result.Value.Data, result.Value.IsHexMode);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 取消事件订阅
        _viewModel.CopyToClipboardRequested -= OnCopyToClipboard;
        _viewModel.PasteFromClipboardRequested -= OnPasteFromClipboard;
        _viewModel.AppendToSendFrameRequested -= OnAppendToSendFrame;
        
        base.OnClosed(e);
    }
}
