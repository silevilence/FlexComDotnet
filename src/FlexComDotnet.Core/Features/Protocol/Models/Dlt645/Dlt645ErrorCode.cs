namespace FlexComDotnet.Core.Features.Protocol.Models.Dlt645;

/// <summary>
/// DL/T 645-2007 错误码
/// </summary>
public enum Dlt645ErrorCode : byte
{
    None = 0x00,
    OtherError = 0x01,
    NoData = 0x02,
    PasswordError = 0x04,
    BaudRateNotSupported = 0x08,
    YearZoneOverflow = 0x10,
    DayZoneOverflow = 0x20,
    RateOverflow = 0x40
}

/// <summary>
/// DL/T 645-2007 错误码扩展方法
/// </summary>
public static class Dlt645ErrorCodeExtensions
{
    public static string GetDescription(this Dlt645ErrorCode errorCode) => errorCode switch
    {
        Dlt645ErrorCode.None => "无错误",
        Dlt645ErrorCode.OtherError => "其他错误",
        Dlt645ErrorCode.NoData => "无请求数据",
        Dlt645ErrorCode.PasswordError => "密码错/未授权",
        Dlt645ErrorCode.BaudRateNotSupported => "通信速率不能更改",
        Dlt645ErrorCode.YearZoneOverflow => "年时区数超",
        Dlt645ErrorCode.DayZoneOverflow => "日时段数超",
        Dlt645ErrorCode.RateOverflow => "费率数超",
        _ => $"未知错误(0x{(byte)errorCode:X2})"
    };

    public static List<string> GetAllErrors(byte errorByte)
    {
        var errors = new List<string>();
        if ((errorByte & 0x01) != 0) errors.Add(Dlt645ErrorCode.OtherError.GetDescription());
        if ((errorByte & 0x02) != 0) errors.Add(Dlt645ErrorCode.NoData.GetDescription());
        if ((errorByte & 0x04) != 0) errors.Add(Dlt645ErrorCode.PasswordError.GetDescription());
        if ((errorByte & 0x08) != 0) errors.Add(Dlt645ErrorCode.BaudRateNotSupported.GetDescription());
        if ((errorByte & 0x10) != 0) errors.Add(Dlt645ErrorCode.YearZoneOverflow.GetDescription());
        if ((errorByte & 0x20) != 0) errors.Add(Dlt645ErrorCode.DayZoneOverflow.GetDescription());
        if ((errorByte & 0x40) != 0) errors.Add(Dlt645ErrorCode.RateOverflow.GetDescription());
        return errors;
    }
}
