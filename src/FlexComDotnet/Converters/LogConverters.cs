using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Logging.Models;

namespace FlexComDotnet.Converters;

/// <summary>
/// 统一日志等级 → 颜色转换器
/// </summary>
public class UnifiedLogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                LogLevel.Info => new SolidColorBrush(Color.FromRgb(86, 182, 194)),
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(229, 192, 123)),
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(224, 108, 117)),
                _ => new SolidColorBrush(Color.FromRgb(212, 212, 212))
            };
        }
        return new SolidColorBrush(Color.FromRgb(212, 212, 212));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 统一日志等级 → 具有形状辨识度的 Emoji 前缀转换器
/// </summary>
public class UnifiedLogLevelToEmojiConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "🔍",
                LogLevel.Info => "💡",
                LogLevel.Warning => "⚠️",
                LogLevel.Error => "❌",
                _ => "📝"
            };
        }
        return "📝";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 统一日志等级 → 标签文字转换器
/// </summary>
public class UnifiedLogLevelToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DBG",
                LogLevel.Info => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                _ => "---"
            };
        }
        return "---";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 日志来源模块 → 中文显示名转换器
/// </summary>
public class LogSourceToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogSource source)
        {
            return source switch
            {
                LogSource.System => "系统",
                LogSource.Serial => "串口",
                LogSource.Network => "网络",
                LogSource.Script => "脚本",
                LogSource.AutoReply => "自动回复",
                LogSource.Protocol => "协议",
                LogSource.Visualization => "可视化",
                _ => source.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
