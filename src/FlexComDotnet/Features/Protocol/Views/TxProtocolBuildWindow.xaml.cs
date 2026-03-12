using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.ViewModels;

namespace FlexComDotnet.Features.Protocol.Views;

/// <summary>
/// 发送区协议组帧浮窗
/// </summary>
public partial class TxProtocolBuildWindow : Window
{
    private readonly IProtocolParserService _parserService;
    private readonly SerialCommunicationViewModel _commViewModel;
    private readonly ObservableCollection<FieldInputItem> _fieldInputs = [];

    public TxProtocolBuildWindow(IProtocolParserService parserService, SerialCommunicationViewModel commViewModel)
    {
        InitializeComponent();
        _parserService = parserService;
        _commViewModel = commViewModel;

        FieldInputsControl.ItemsSource = _fieldInputs;
        ProtocolComboBox.ItemsSource = _parserService.GetAllDefinitions();
    }

    private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshFieldInputs();
        TryReverseParseFromSendBox();
    }

    private void RefreshFieldInputs()
    {
        _fieldInputs.Clear();
        BuildResultTextBox.Text = string.Empty;
        StatusText.Text = string.Empty;

        if (ProtocolComboBox.SelectedItem is not FrameDefinition definition)
            return;

        // DL/T 645 特殊字段
        if (definition.ProtocolType == ProtocolType.Dlt645)
        {
            _fieldInputs.Add(new FieldInputItem
            {
                FieldName = "电表地址",
                DisplayName = "电表地址",
                Description = "12位BCD码地址",
                DataType = DataType.AsciiString,
                DefaultValue = "000000000000"
            });
            _fieldInputs.Add(new FieldInputItem
            {
                FieldName = "控制码",
                DisplayName = "控制码",
                Description = "功能控制字节 (Hex)",
                DataType = DataType.UInt8,
                DefaultValue = "11"
            });
            _fieldInputs.Add(new FieldInputItem
            {
                FieldName = "数据标识",
                DisplayName = "数据标识",
                Description = "4字节数据标识 (十进制)",
                DataType = DataType.UInt32,
                DefaultValue = "65536"
            });
        }

        // 用户定义字段
        foreach (var field in definition.Fields.Where(f => f.IsEnabled))
        {
            _fieldInputs.Add(new FieldInputItem
            {
                FieldName = field.Name,
                DisplayName = field.Name,
                Description = field.Description,
                DataType = field.DataType,
                DefaultValue = string.Empty,
                IsHexMode = field.DataType is DataType.Bytes or DataType.UInt8
            });
        }
    }

    /// <summary>
    /// 尝试用当前发送区内容反向解析到字段输入框
    /// </summary>
    private void TryReverseParseFromSendBox()
    {
        if (ProtocolComboBox.SelectedItem is not FrameDefinition definition)
            return;

        var sendText = _commViewModel.SendText;
        if (string.IsNullOrWhiteSpace(sendText))
            return;

        // 尝试将发送区内容作为 Hex 解析
        byte[]? bytes = null;
        if (_commViewModel.IsHexSendMode && HexHelper.IsValidHexString(sendText))
        {
            bytes = HexHelper.HexStringToBytes(sendText);
        }
        else if (!_commViewModel.IsHexSendMode)
        {
            // ASCII 模式也尝试 Hex 解析
            if (HexHelper.IsValidHexString(sendText))
            {
                bytes = HexHelper.HexStringToBytes(sendText);
            }
        }

        if (bytes == null || bytes.Length == 0)
            return;

        var parser = _parserService.GetParser(definition.Name);
        if (parser == null || !parser.Validate(bytes))
            return;

        var result = parser.Parse(bytes);
        if (!result.IsValid)
            return;

        // 反向填充字段
        foreach (var input in _fieldInputs)
        {
            var field = result.Fields.Find(f => f.Name.Equals(input.FieldName, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                input.Value = input.IsHexMode ? field.HexValue : field.DisplayValue;
            }
        }
        StatusText.Text = "已从发送区反向解析并填充字段";
    }

    private void ReverseParse_Click(object sender, RoutedEventArgs e)
    {
        TryReverseParseFromSendBox();
    }

    private void Build_Click(object sender, RoutedEventArgs e)
    {
        DoBuild();
    }

    private void DoBuild()
    {
        if (ProtocolComboBox.SelectedItem is not FrameDefinition definition)
        {
            StatusText.Text = "请先选择协议";
            return;
        }

        var parser = _parserService.GetParser(definition.Name);
        if (parser == null)
        {
            StatusText.Text = "未找到对应的解析器";
            return;
        }

        try
        {
            var fieldValues = new Dictionary<string, object>();
            foreach (var input in _fieldInputs)
            {
                if (!string.IsNullOrEmpty(input.Value))
                {
                    fieldValues[input.FieldName] = input.IsHexMode
                        ? (object)HexHelper.HexStringToBytes(input.Value.Replace(" ", ""))
                        : input.Value;
                }
            }

            var frame = parser.BuildFrame(fieldValues);
            BuildResultTextBox.Text = HexHelper.BytesToHexString(frame);
            StatusText.Text = $"组帧成功: {frame.Length} 字节";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"组帧失败: {ex.Message}";
            BuildResultTextBox.Text = string.Empty;
        }
    }

    private void OverwriteBackfill_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BuildResultTextBox.Text))
        {
            DoBuild();
            if (string.IsNullOrWhiteSpace(BuildResultTextBox.Text))
                return;
        }

        _commViewModel.SwitchToHexModeWithConversion();
        _commViewModel.SendText = BuildResultTextBox.Text;
        StatusText.Text = "已覆盖回填至发送区 (Hex 模式)";
    }

    private void AppendBackfill_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BuildResultTextBox.Text))
        {
            DoBuild();
            if (string.IsNullOrWhiteSpace(BuildResultTextBox.Text))
                return;
        }

        _commViewModel.SwitchToHexModeWithConversion();
        var existing = _commViewModel.SendText;
        if (!string.IsNullOrEmpty(existing) && !existing.EndsWith(' '))
        {
            _commViewModel.SendText = existing + " " + BuildResultTextBox.Text;
        }
        else
        {
            _commViewModel.SendText = existing + BuildResultTextBox.Text;
        }
        StatusText.Text = "已追加回填至发送区 (Hex 模式)";
    }
}
