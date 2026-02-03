using System.Globalization;
using System.Windows.Data;
using FlexComDotnet.Core.Features.Network.Models;

namespace FlexComDotnet.Converters;

/// <summary>
/// 连接类型显示转换器
/// </summary>
public class ConnectionTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionType connectionType)
        {
            return connectionType switch
            {
                ConnectionType.Serial => "串口",
                ConnectionType.TcpClient => "TCP 客户端",
                ConnectionType.TcpServer => "TCP 服务器",
                ConnectionType.Udp => "UDP",
                _ => connectionType.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 连接状态显示转换器
/// </summary>
public class ConnectionStateDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Disconnected => "未连接",
                ConnectionState.Connecting => "连接中...",
                ConnectionState.Connected => "已连接",
                ConnectionState.Listening => "监听中",
                ConnectionState.Error => "错误",
                _ => state.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 连接类型到可见性转换器
/// </summary>
public class ConnectionTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionType selectedType && parameter is string targetTypes)
        {
            var types = targetTypes.Split(',');
            foreach (var type in types)
            {
                if (Enum.TryParse<ConnectionType>(type.Trim(), out var targetType2) && selectedType == targetType2)
                {
                    return System.Windows.Visibility.Visible;
                }
            }
            return System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
