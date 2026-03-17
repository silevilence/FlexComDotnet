using System.Globalization;
using System.Windows.Data;
using FlexComDotnet.Core.Features.Protocol.Models;

namespace FlexComDotnet.Converters;

/// <summary>
/// 协议类型显示转换器
/// </summary>
public class ProtocolTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProtocolType protocolType)
        {
            return protocolType switch
            {
                ProtocolType.Generic => "通用协议",
                ProtocolType.Dlt645 => "DL/T 645-2007",
                ProtocolType.ModbusRtu => "Modbus-RTU",
                _ => protocolType.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
