using System.Globalization;
using System.Windows.Data;
using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Converters;

/// <summary>
/// 波特率显示转换器
/// </summary>
public class BaudRateDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BaudRate baudRate)
        {
            return ((int)baudRate).ToString();
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 数据位显示转换器
/// </summary>
public class DataBitsDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataBitsOption dataBits)
        {
            return ((int)dataBits).ToString();
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 停止位显示转换器
/// </summary>
public class StopBitsDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StopBitsOption stopBits)
        {
            return stopBits switch
            {
                StopBitsOption.One => "1",
                StopBitsOption.OnePointFive => "1.5",
                StopBitsOption.Two => "2",
                _ => stopBits.ToString()
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
/// 校验位显示转换器
/// </summary>
public class ParityDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ParityOption parity)
        {
            return parity switch
            {
                ParityOption.None => "None",
                ParityOption.Odd => "Odd",
                ParityOption.Even => "Even",
                ParityOption.Mark => "Mark",
                ParityOption.Space => "Space",
                _ => parity.ToString()
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
/// 流控显示转换器
/// </summary>
public class FlowControlDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FlowControlOption flowControl)
        {
            return flowControl switch
            {
                FlowControlOption.None => "None",
                FlowControlOption.XonXoff => "XON/XOFF",
                FlowControlOption.RtsCts => "RTS/CTS",
                FlowControlOption.DtrDsr => "DTR/DSR",
                _ => flowControl.ToString()
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
/// 布尔值取反转换器
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}

/// <summary>
/// 校验和类型显示转换器
/// </summary>
public class ChecksumTypeToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChecksumType checksumType)
        {
            return checksumType switch
            {
                ChecksumType.None => "无",
                ChecksumType.Sum8 => "Sum8",
                ChecksumType.Crc16Modbus => "CRC16-MODBUS",
                _ => checksumType.ToString()
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
/// 布尔值到文本转换器
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string texts)
        {
            var parts = texts.Split('|');
            if (parts.Length >= 2)
            {
                return boolValue ? parts[0] : parts[1];
            }
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
