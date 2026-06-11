using System.Globalization;
using System.Windows.Data;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.ViewModels;

namespace FlexComDotnet.Converters;

/// <summary>
/// DataRecord 格式化转换器 — 将 DataRecord 格式化为显示字符串
/// 仅对当前可见项执行，延迟格式化避免全量重建
/// </summary>
public class DataRecordFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DataRecord record)
            return string.Empty;

        // 前缀
        var prefix = record.IsTx ? "[TX] " : "[RX] ";
        if (record.RecordType == DataRecordType.ScriptAutoReply)
            prefix = "[⚡]";
        else if (record.RecordType == DataRecordType.AutoReply)
            prefix = "[↩️]";

        // 时间戳
        if (RecordDisplaySettings.ShowTimestamp)
        {
            var format = RecordDisplaySettings.ShowDateInTimestamp ? "yyyy-MM-dd HH:mm:ss.fff" : "HH:mm:ss.fff";
            var timestamp = record.Timestamp.ToString(format);
            prefix = $"[{timestamp}] {prefix}";
        }

        // 数据格式化
        string dataStr;
        if (RecordDisplaySettings.IsHexDisplayMode)
        {
            dataStr = HexHelper.BytesToHexString(record.Data);
        }
        else
        {
            dataStr = HexHelper.BytesToAsciiString(record.Data, '.');
        }

        // Hook/自动应答且数据有变化 → 显示原始→处理后
        if ((record.RecordType == DataRecordType.HookProcessed
             || record.RecordType == DataRecordType.ScriptAutoReply
             || record.RecordType == DataRecordType.AutoReply)
            && record.OriginalData != null
            && !record.Data.SequenceEqual(record.OriginalData))
        {
            string originalStr;
            if (RecordDisplaySettings.IsHexDisplayMode)
            {
                originalStr = HexHelper.BytesToHexString(record.OriginalData);
            }
            else
            {
                originalStr = HexHelper.BytesToAsciiString(record.OriginalData, '.');
            }

            return prefix + $"⬇ {originalStr} ➡ ⬆ {dataStr}";
        }

        return prefix + dataStr;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
