namespace FlexComDotnet.Core.Features.Serial.ViewModels;

/// <summary>
/// 显示设置静态持有 — 由 ViewModel 在属性变更时更新，供转换器读取
/// </summary>
public static class RecordDisplaySettings
{
    /// <summary>
    /// 是否使用 Hex 显示模式
    /// </summary>
    public static bool IsHexDisplayMode { get; set; }

    /// <summary>
    /// 是否显示时间戳
    /// </summary>
    public static bool ShowTimestamp { get; set; }

    /// <summary>
    /// 时间戳是否显示日期
    /// </summary>
    public static bool ShowDateInTimestamp { get; set; }

    /// <summary>
    /// 是否自动换行（控制 TextWrapping）
    /// </summary>
    public static bool AutoLineBreak { get; set; }
}
