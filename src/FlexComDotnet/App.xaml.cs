using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Protocol.Services;

namespace FlexComDotnet;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 服务提供者
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 修复菜单弹出方向问题 (某些系统设置会导致菜单从左边弹出)
        FixMenuDropAlignment();

        // 配置依赖注入
        var services = new ServiceCollection();
        services.AddAppServices();
        Services = services.BuildServiceProvider();

        // 初始化主题服务
        var themeService = Services.GetRequiredService<IThemeService>() as ThemeService;
        themeService?.Initialize();

        // 加载协议定义
        LoadProtocolDefinitions();

        // 创建并显示主窗口
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    /// <summary>
    /// 修复 MenuDropAlignment 导致的菜单弹出方向问题
    /// </summary>
    private static void FixMenuDropAlignment()
    {
        var field = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null && SystemParameters.MenuDropAlignment)
        {
            field.SetValue(null, false);
        }
    }

    private static void LoadProtocolDefinitions()
    {
        var configService = Services.GetRequiredService<IConfigurationService>();
        var parserService = Services.GetRequiredService<IProtocolParserService>();
        
        var config = configService.Load();
        if (config.ProtocolDefinitions.Count > 0)
        {
            parserService.LoadDefinitions(config.ProtocolDefinitions);
        }
    }
}

