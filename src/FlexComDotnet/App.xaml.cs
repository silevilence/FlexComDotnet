using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Services;

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

        // 配置依赖注入
        var services = new ServiceCollection();
        services.AddAppServices();
        Services = services.BuildServiceProvider();

        // 初始化主题服务
        var themeService = Services.GetRequiredService<IThemeService>() as ThemeService;
        themeService?.Initialize();

        // 创建并显示主窗口
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

