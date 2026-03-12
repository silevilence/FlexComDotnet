using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Features.Protocol.Views;

/// <summary>
/// 接收区协议解析浮窗
/// </summary>
public partial class RxProtocolParseWindow : Window
{
    private readonly IProtocolParserService _parserService;

    public RxProtocolParseWindow(IProtocolParserService parserService, string initialHexData = "")
    {
        InitializeComponent();
        _parserService = parserService;

        // 加载协议列表
        ProtocolComboBox.ItemsSource = _parserService.GetAllDefinitions();

        // 预填充 Hex 数据
        if (!string.IsNullOrWhiteSpace(initialHexData))
        {
            HexInputTextBox.Text = initialHexData;
        }
    }

    private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 协议变更后自动重新解析（如果有数据）
        if (!string.IsNullOrWhiteSpace(HexInputTextBox.Text))
        {
            DoParse();
        }
    }

    private void Parse_Click(object sender, RoutedEventArgs e)
    {
        DoParse();
    }

    private void AutoDetect_Click(object sender, RoutedEventArgs e)
    {
        var hex = HexInputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(hex))
        {
            StatusText.Text = "请先输入 Hex 数据";
            return;
        }

        if (!HexHelper.IsValidHexString(hex))
        {
            StatusText.Text = "无效的十六进制格式";
            return;
        }

        var bytes = HexHelper.HexStringToBytes(hex);
        var result = _parserService.AutoParse(bytes);
        if (result != null && result.IsValid)
        {
            // 找到匹配的协议，自动选中
            var definitions = _parserService.GetAllDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Name == result.ProtocolName)
                {
                    ProtocolComboBox.SelectedIndex = i;
                    break;
                }
            }
            ShowParseResult(result);
        }
        else
        {
            StatusText.Text = "自动检测失败：未找到匹配的协议";
            FieldsDataGrid.ItemsSource = null;
            ChecksumStatusText.Text = string.Empty;
        }
    }

    private void DoParse()
    {
        var hex = HexInputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(hex))
        {
            StatusText.Text = "请输入 Hex 数据";
            return;
        }

        if (!HexHelper.IsValidHexString(hex))
        {
            StatusText.Text = "无效的十六进制格式";
            return;
        }

        var bytes = HexHelper.HexStringToBytes(hex);

        ParsedFrame? result;
        if (ProtocolComboBox.SelectedItem is FrameDefinition selected)
        {
            result = _parserService.Parse(selected.Name, bytes);
        }
        else
        {
            result = _parserService.AutoParse(bytes);
        }

        if (result != null)
        {
            ShowParseResult(result);
        }
        else
        {
            StatusText.Text = "解析失败";
            FieldsDataGrid.ItemsSource = null;
            ChecksumStatusText.Text = string.Empty;
        }
    }

    private void ShowParseResult(ParsedFrame result)
    {
        if (result.IsValid)
        {
            StatusText.Text = $"解析成功 — 协议: {result.ProtocolName}，{result.Fields.Count} 个字段";
            FieldsDataGrid.ItemsSource = result.Fields;
            UpdateValueColumnBinding();
            ChecksumStatusText.Text = result.ChecksumValid ? "✅ 校验通过" : "❌ 校验失败";
        }
        else
        {
            StatusText.Text = $"解析失败: {result.ErrorMessage}";
            FieldsDataGrid.ItemsSource = null;
            ChecksumStatusText.Text = string.Empty;
        }
    }

    private void HexDisplayToggle_Changed(object sender, RoutedEventArgs e)
    {
        UpdateValueColumnBinding();
    }

    private void UpdateValueColumnBinding()
    {
        // 值列是 DataGrid 的第二列 (index 1)
        if (FieldsDataGrid.Columns.Count > 1 && FieldsDataGrid.Columns[1] is DataGridTextColumn valueColumn)
        {
            var isHex = HexDisplayToggle.IsChecked == true;
            valueColumn.Binding = new Binding(isHex ? "HexValue" : "DisplayValue");
        }
    }
}
