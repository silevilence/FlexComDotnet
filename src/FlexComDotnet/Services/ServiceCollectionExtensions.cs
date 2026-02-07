using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Core.Features.Layout.Services;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Checksum.ViewModels;
using FlexComDotnet.Core.Features.AutoReply.Services;
using FlexComDotnet.Core.Features.AutoReply.ViewModels;
using FlexComDotnet.Core.Features.Network.Services;
using FlexComDotnet.Core.Features.Network.ViewModels;
using FlexComDotnet.Core.Features.Update.Services;
using FlexComDotnet.Core.Features.Update.ViewModels;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Scripting.ViewModels;

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

        // 网络服务 (单例)
        services.AddSingleton<ITcpClientService, TcpClientService>();
        services.AddSingleton<ITcpServerService, TcpServerService>();
        services.AddSingleton<IUdpService, UdpService>();

        // 日志保存服务 (单例)
        services.AddSingleton<ILogSaveService, LogSaveService>();

        // 指令存储服务 (单例)
        services.AddSingleton<ICommandStorageService, LiteDbCommandStorageService>();

        // 面板管理器 (单例)
        services.AddSingleton<IPanelManager, PanelManager>();

        // 校验和服务 (单例)
        services.AddSingleton<IChecksumService, ChecksumService>();

        // 自动回复服务 (单例)
        services.AddSingleton<IAutoReplyService, AutoReplyService>();

        // 更新服务 (单例)
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IGitHubReleaseService, GitHubReleaseService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // 脚本服务 (单例)
        services.AddSingleton<IScriptApiBridge, ScriptApiBridge>();
        services.AddSingleton<IScriptEngine>(sp =>
        {
            var engine = new ScriptEngine();
            var bridge = sp.GetRequiredService<IScriptApiBridge>();
            engine.RegisterApiBridge(bridge);
            return engine;
        });
        services.AddSingleton<IScriptManager>(sp =>
        {
            var scriptsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts");
            return new ScriptManager(scriptsDir);
        });

        // ViewModels (瞬态)
        services.AddTransient<SerialConfigViewModel>();
        services.AddTransient<SerialCommunicationViewModel>();
        services.AddTransient<CommandListViewModel>();
        services.AddTransient<ChecksumCalculatorViewModel>();
        services.AddTransient<AutoReplyViewModel>();
        services.AddTransient<ConnectionConfigViewModel>();
        services.AddTransient<UpdateViewModel>();
        services.AddTransient<ScriptingViewModel>();

        return services;
    }
}
