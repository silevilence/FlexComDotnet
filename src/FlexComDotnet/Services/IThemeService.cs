namespace FlexComDotnet.Services;

/// <summary>
/// 主题类型枚举 (实际显示的主题)
/// </summary>
public enum ThemeType
{
    /// <summary>
    /// 浅色主题 (白天模式)
    /// </summary>
    Light,

    /// <summary>
    /// 深色主题 (夜间模式)
    /// </summary>
    Dark
}

/// <summary>
/// 主题模式枚举 (用户选择的模式)
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// 浅色模式
    /// </summary>
    Light,

    /// <summary>
    /// 深色模式
    /// </summary>
    Dark,

    /// <summary>
    /// 跟随系统
    /// </summary>
    System
}

/// <summary>
/// 主题服务接口
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 获取当前实际显示的主题
    /// </summary>
    ThemeType CurrentTheme { get; }

    /// <summary>
    /// 获取当前主题模式 (用户选择)
    /// </summary>
    ThemeMode CurrentMode { get; }

    /// <summary>
    /// 主题变更事件
    /// </summary>
    event EventHandler<ThemeType>? ThemeChanged;

    /// <summary>
    /// 主题模式变更事件
    /// </summary>
    event EventHandler<ThemeMode>? ModeChanged;

    /// <summary>
    /// 设置主题 (向后兼容)
    /// </summary>
    /// <param name="theme">要设置的主题类型</param>
    void SetTheme(ThemeType theme);

    /// <summary>
    /// 设置主题模式
    /// </summary>
    /// <param name="mode">主题模式</param>
    void SetMode(ThemeMode mode);

    /// <summary>
    /// 切换主题模式 (浅色→深色→跟随系统→浅色)
    /// </summary>
    void CycleMode();

    /// <summary>
    /// 切换主题 (在深色和浅色之间切换，向后兼容)
    /// </summary>
    void ToggleTheme();

    /// <summary>
    /// 判断当前是否为深色主题
    /// </summary>
    bool IsDarkTheme { get; }
}
