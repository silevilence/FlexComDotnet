using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.Checksum.ViewModels;

/// <summary>
/// 校验和计算器 ViewModel
/// </summary>
public partial class ChecksumCalculatorViewModel : ObservableObject
{
    private readonly IChecksumService _checksumService;

    /// <summary>
    /// 可用算法列表
    /// </summary>
    public ObservableCollection<IChecksumAlgorithm> AvailableAlgorithms { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private IChecksumAlgorithm? _selectedAlgorithm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AsciiPreview))]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _resultHex = string.Empty;

    [ObservableProperty]
    private string _resultDecimal = string.Empty;

    [ObservableProperty]
    private string _algorithmInfo = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// ASCII 预览 (将 Hex 转换为 ASCII 显示)
    /// </summary>
    public string AsciiPreview
    {
        get
        {
            try
            {
                var bytes = GetInputBytes();
                return HexHelper.BytesToAsciiString(bytes, '.');
            }
            catch
            {
                return "无效输入";
            }
        }
    }

    /// <summary>
    /// 请求将结果附加到发送帧的事件
    /// </summary>
    public event EventHandler<byte[]>? AppendToSendFrameRequested;

    /// <summary>
    /// 请求复制到剪贴板的事件
    /// </summary>
    public event EventHandler<string>? CopyToClipboardRequested;

    /// <summary>
    /// 请求从剪贴板粘贴的事件
    /// </summary>
    public event EventHandler<Action<string?>>? PasteFromClipboardRequested;

    public ChecksumCalculatorViewModel(IChecksumService checksumService)
    {
        _checksumService = checksumService;
        
        LoadAlgorithms();
    }

    private void LoadAlgorithms()
    {
        AvailableAlgorithms.Clear();
        foreach (var algorithm in _checksumService.GetAllAlgorithms())
        {
            AvailableAlgorithms.Add(algorithm);
        }

        // 默认选择第一个算法
        SelectedAlgorithm = AvailableAlgorithms.FirstOrDefault();
    }

    partial void OnSelectedAlgorithmChanged(IChecksumAlgorithm? value)
    {
        if (value != null)
        {
            AlgorithmInfo = $"{value.DisplayName}\n{value.Description}\n输出长度: {value.ResultLength} 字节";
        }
        else
        {
            AlgorithmInfo = string.Empty;
        }

        // 如果已有输入，自动重新计算
        if (!string.IsNullOrEmpty(InputText))
        {
            Calculate();
        }
    }

    /// <summary>
    /// 获取输入数据的字节数组 (强制 Hex 输入)
    /// </summary>
    private byte[] GetInputBytes()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            return [];
        }

        return HexHelper.HexStringToBytes(InputText);
    }

    /// <summary>
    /// 计算校验值
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private void Calculate()
    {
        ErrorMessage = string.Empty;

        if (SelectedAlgorithm == null)
        {
            ErrorMessage = "请选择一个算法";
            return;
        }

        try
        {
            var inputBytes = GetInputBytes();
            var resultBytes = SelectedAlgorithm.Calculate(inputBytes);

            // 十六进制结果
            ResultHex = HexHelper.BytesToHexString(resultBytes);

            // 十进制结果 (仅对小于等于8字节的结果有意义)
            if (resultBytes.Length <= 8)
            {
                ulong decValue = 0;
                // 按大端序计算
                foreach (var b in resultBytes)
                {
                    decValue = (decValue << 8) | b;
                }
                ResultDecimal = decValue.ToString();
            }
            else
            {
                ResultDecimal = "(结果过长，不显示十进制)";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"计算错误: {ex.Message}";
            ResultHex = string.Empty;
            ResultDecimal = string.Empty;
        }
    }

    private bool CanCalculate() => SelectedAlgorithm != null;

    /// <summary>
    /// 清空输入
    /// </summary>
    [RelayCommand]
    private void ClearInput()
    {
        InputText = string.Empty;
        ResultHex = string.Empty;
        ResultDecimal = string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>
    /// 将结果附加到发送帧
    /// </summary>
    [RelayCommand]
    private void AppendResultToSendFrame()
    {
        if (string.IsNullOrEmpty(ResultHex))
        {
            ErrorMessage = "请先计算结果";
            return;
        }

        var resultBytes = HexHelper.HexStringToBytes(ResultHex);
        AppendToSendFrameRequested?.Invoke(this, resultBytes);
    }

    /// <summary>
    /// 从发送帧导入数据 (支持 Hex 或 ASCII，自动转换为 Hex)
    /// </summary>
    /// <param name="data">输入数据（Hex 或 ASCII）</param>
    /// <param name="isHex">true 表示 Hex 格式，false 表示 ASCII 格式</param>
    public void ImportFromSendFrame(string? data, bool isHex)
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        if (isHex)
        {
            // Hex 格式直接使用
            InputText = data;
        }
        else
        {
            // ASCII 格式转换为 Hex
            var bytes = System.Text.Encoding.ASCII.GetBytes(data);
            InputText = HexHelper.BytesToHexString(bytes);
        }
        
        Calculate();
    }

    /// <summary>
    /// 复制结果到剪贴板
    /// </summary>
    [RelayCommand]
    private void CopyResult()
    {
        if (!string.IsNullOrEmpty(ResultHex))
        {
            CopyToClipboardRequested?.Invoke(this, ResultHex);
        }
    }

    /// <summary>
    /// 从剪贴板粘贴
    /// </summary>
    [RelayCommand]
    private void PasteFromClipboard()
    {
        PasteFromClipboardRequested?.Invoke(this, text =>
        {
            if (!string.IsNullOrEmpty(text))
            {
                InputText = text;
                Calculate();
            }
        });
    }
}
