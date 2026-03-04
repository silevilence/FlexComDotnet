using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
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

    /// <summary>
    /// 崩溃日志文件路径
    /// </summary>
    private static readonly string CrashLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 注册全局异常处理器
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

    /// <summary>
    /// UI 线程未处理异常
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);

        // 对于布局相关的 IndexOutOfRangeException，标记为已处理以防止崩溃
        if (e.Exception is IndexOutOfRangeException && e.Exception.StackTrace?.Contains("Grid.SetFinalSizeMaxDiscrepancy") == true)
        {
            Debug.WriteLine($"[Layout] 捕获到 WPF Grid 布局异常（已忽略）: {e.Exception.Message}");
            e.Handled = true;
            return;
        }

        e.Handled = true;
        MessageBox.Show(
            $"发生未处理的异常，详细信息已写入日志文件:\n{CrashLogPath}\n\n{e.Exception.Message}",
            "错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <summary>
    /// 非 UI 线程未处理异常
    /// </summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog("UnhandledException", ex);
        }
    }

    /// <summary>
    /// Task 未观察到的异常
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// 将异常信息写入崩溃日志文件
    /// </summary>
    private static void WriteCrashLog(string source, Exception ex)
    {
        try
        {
            var logEntry = $"""
            ========================================
            [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}
            Exception: {ex.GetType().FullName}
            Message: {ex.Message}
            StackTrace:
            {ex.StackTrace}
            ========================================

            """;

            File.AppendAllText(CrashLogPath, logEntry);
            Debug.WriteLine($"[CrashLog] {source}: {ex.Message}");
        }
        catch
        {
            // 日志写入失败时不再抛出
        }
    }
}

