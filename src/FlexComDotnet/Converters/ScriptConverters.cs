using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Scripting.Models;

namespace FlexComDotnet.Converters;

/// <summary>
/// 脚本日志级别 → 前景色转换器
/// </summary>
public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScriptLogLevel level)
        {
            return level switch
            {
                ScriptLogLevel.Debug => new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                ScriptLogLevel.Info => new SolidColorBrush(Color.FromRgb(86, 182, 194)),
                ScriptLogLevel.Warning => new SolidColorBrush(Color.FromRgb(229, 192, 123)),
                ScriptLogLevel.Error => new SolidColorBrush(Color.FromRgb(224, 108, 117)),
                _ => new SolidColorBrush(Colors.White)
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 脚本日志级别 → 标签文本转换器
/// </summary>
public class LogLevelToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScriptLogLevel level)
        {
            return level switch
            {
                ScriptLogLevel.Debug => "DBG",
                ScriptLogLevel.Info => "INF",
                ScriptLogLevel.Warning => "WRN",
                ScriptLogLevel.Error => "ERR",
                _ => "???"
            };
        }
        return "???";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
