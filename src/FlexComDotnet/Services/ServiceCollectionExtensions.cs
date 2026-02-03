using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Core.Features.Layout.Services;

namespace FlexComDotnet.Services;

/// <summary>
/// 服务容器配置
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册应用程序服务
    /// </summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // 主题服务 (单例)
        services.AddSingleton<IThemeService, ThemeService>();

        // 配置服务 (单例)
        services.AddSingleton<IConfigurationService, JsonConfigurationService>();
        
        // 串口服务 (单例)
        services.AddSingleton<ISerialPortService, SerialPortService>();

        // 日志保存服务 (单例)
        services.AddSingleton<ILogSaveService, LogSaveService>();

        // 指令存储服务 (单例)
        services.AddSingleton<ICommandStorageService, LiteDbCommandStorageService>();

        // 面板管理器 (单例)
        services.AddSingleton<IPanelManager, PanelManager>();

        // ViewModels (瞬态)
        services.AddTransient<SerialConfigViewModel>();
        services.AddTransient<SerialCommunicationViewModel>();
        services.AddTransient<CommandListViewModel>();

        return services;
    }
}
