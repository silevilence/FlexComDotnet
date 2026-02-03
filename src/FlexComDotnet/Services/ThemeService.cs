using System.Windows;
using Microsoft.Win32;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Services;

/// <summary>
/// 主题服务实现
/// </summary>
public class ThemeService : IThemeService
{
    private const string LightThemeUri = "Themes/LightTheme.xaml";
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private readonly IConfigurationService _configurationService;
    private ThemeType _currentTheme = ThemeType.Dark; // 当前实际显示的主题
    private ThemeMode _currentMode = ThemeMode.Dark;  // 用户选择的模式，默认深色

    /// <summary>
    /// 构造函数
    /// </summary>
    public ThemeService(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    /// <inheritdoc/>
    public ThemeType CurrentTheme => _currentTheme;

    /// <inheritdoc/>
    public ThemeMode CurrentMode => _currentMode;

    /// <inheritdoc/>
    public bool IsDarkTheme => _currentTheme == ThemeType.Dark;

    /// <inheritdoc/>
    public event EventHandler<ThemeType>? ThemeChanged;

    /// <inheritdoc/>
    public event EventHandler<ThemeMode>? ModeChanged;

    /// <summary>
    /// 初始化主题服务，应用默认主题
    /// </summary>
    public void Initialize()
    {
        // 监听系统主题变化
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
        
        // 从配置中加载主题模式
        LoadThemeModeFromConfig();
        
        // 应用主题
        ApplyMode(_currentMode);
    }

    /// <summary>
    /// 从配置中加载主题模式
    /// </summary>
    private void LoadThemeModeFromConfig()
    {
        var config = _configurationService.Load();
        _currentMode = config.DisplayConfig.ThemeMode switch
        {
            0 => ThemeMode.Light,
            1 => ThemeMode.Dark,
            2 => ThemeMode.System,
            _ => ThemeMode.Dark
        };
    }

    /// <summary>
    /// 保存主题模式到配置
    /// </summary>
    private void SaveThemeModeToConfig()
    {
        var config = _configurationService.Load();
        config.DisplayConfig.ThemeMode = _currentMode switch
        {
            ThemeMode.Light => 0,
            ThemeMode.Dark => 1,
            ThemeMode.System => 2,
            _ => 1
        };
        _configurationService.Save(config);
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
    }

    /// <inheritdoc/>
    public void SetTheme(ThemeType theme)
    {
        // 向后兼容：直接设置主题会切换到对应模式
        var mode = theme == ThemeType.Light ? ThemeMode.Light : ThemeMode.Dark;
        SetMode(mode);
    }

    /// <inheritdoc/>
    public void SetMode(ThemeMode mode)
    {
        if (_currentMode == mode)
            return;

        _currentMode = mode;
        ModeChanged?.Invoke(this, mode);
        
        // 保存到配置
        SaveThemeModeToConfig();
        
        ApplyMode(mode);
    }

    /// <inheritdoc/>
    public void CycleMode()
    {
        var newMode = _currentMode switch
        {
            ThemeMode.Light => ThemeMode.Dark,
            ThemeMode.Dark => ThemeMode.System,
            ThemeMode.System => ThemeMode.Light,
            _ => ThemeMode.Dark
        };
        SetMode(newMode);
    }

    /// <inheritdoc/>
    public void ToggleTheme()
    {
        var newTheme = _currentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light;
        SetTheme(newTheme);
    }

    /// <summary>
    /// 获取系统当前主题
    /// </summary>
    private ThemeType GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            if (value is int intValue)
            {
                // 1 = Light theme, 0 = Dark theme
                return intValue == 1 ? ThemeType.Light : ThemeType.Dark;
            }
        }
        catch
        {
            // 无法读取注册表时默认深色
        }
        return ThemeType.Dark;
    }

    /// <summary>
    /// 系统主题变化回调
    /// </summary>
    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentMode == ThemeMode.System)
        {
            // 在 UI 线程上应用主题
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyMode(ThemeMode.System);
            });
        }
    }

    /// <summary>
    /// 应用主题模式
    /// </summary>
    private void ApplyMode(ThemeMode mode)
    {
        var theme = mode switch
        {
            ThemeMode.Light => ThemeType.Light,
            ThemeMode.Dark => ThemeType.Dark,
            ThemeMode.System => GetSystemTheme(),
            _ => ThemeType.Dark
        };

        if (_currentTheme != theme)
        {
            _currentTheme = theme;
            ApplyTheme(theme);
            ThemeChanged?.Invoke(this, theme);
        }
        else if (_currentMode == ThemeMode.System)
        {
            // 即使主题没变，也要应用一次确保正确
            ApplyTheme(theme);
        }
    }

    /// <summary>
    /// 应用指定主题到应用程序
    /// </summary>
    private void ApplyTheme(ThemeType theme)
    {
        var themeUri = theme == ThemeType.Light ? LightThemeUri : DarkThemeUri;
        var resourceUri = new Uri(themeUri, UriKind.Relative);

        // 获取应用程序资源字典
        var appResources = Application.Current.Resources;

        // 查找并移除现有的主题资源
        ResourceDictionary? existingTheme = null;
        foreach (var dict in appResources.MergedDictionaries)
        {
            if (dict.Source != null &&
                (dict.Source.OriginalString.Contains("LightTheme") ||
                 dict.Source.OriginalString.Contains("DarkTheme")))
            {
                existingTheme = dict;
                break;
            }
        }

        if (existingTheme != null)
        {
            appResources.MergedDictionaries.Remove(existingTheme);
        }

        // 添加新主题
        var newTheme = new ResourceDictionary
        {
            Source = resourceUri
        };
        appResources.MergedDictionaries.Add(newTheme);
    }
}
