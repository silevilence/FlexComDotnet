using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.ViewModels;

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
        // 串口服务 (单例)
        services.AddSingleton<ISerialPortService, SerialPortService>();

        // ViewModels (瞬态)
        services.AddTransient<SerialConfigViewModel>();
        services.AddTransient<SerialCommunicationViewModel>();

        return services;
    }
}
