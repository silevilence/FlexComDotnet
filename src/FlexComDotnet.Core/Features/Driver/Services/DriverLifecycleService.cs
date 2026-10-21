using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using System.Management;

namespace FlexComDotnet.Core.Features.Driver.Services;

/// <summary>
/// SerialMonitor 驱动安装、启动、停止与卸载。
/// </summary>
public sealed class DriverLifecycleService
{
    internal const string ServiceName = "SerialMonitor";

    private const string DisplayName = "Serial Monitor Filter Driver";
    private const int CommandTimeoutMilliseconds = 30_000;
    private readonly string? _packageDirectory;
    private readonly Func<string, IReadOnlyList<string>, DriverCommandResult> _runCommand;
    private readonly Func<bool> _isAdministrator;
    private readonly IPnpFilterRegistration _pnpFilterRegistration;
    private readonly Func<string, string?> _resolvePnpInstanceId;

    public DriverLifecycleService()
        : this(null, RunCommand, IsCurrentProcessAdministrator, new DeviceInstanceUpperFilterRegistration(), ResolvePnpInstanceId)
    {
    }

    internal DriverLifecycleService(
        string? packageDirectory,
        Func<string, IReadOnlyList<string>, DriverCommandResult> runCommand,
        Func<bool> isAdministrator,
        IPnpFilterRegistration? pnpFilterRegistration = null,
        Func<string, string?>? resolvePnpInstanceId = null)
    {
        _packageDirectory = packageDirectory;
        _runCommand = runCommand;
        _isAdministrator = isAdministrator;
        _pnpFilterRegistration = pnpFilterRegistration ?? NoopPnpFilterRegistration.Instance;
        _resolvePnpInstanceId = resolvePnpInstanceId ?? ResolvePnpInstanceId;
    }

    /// <summary>
    /// 检测驱动服务；不存在则安装；最后确保服务已启动。
    /// </summary>
    public DriverLifecycleResult EnsureInstalledAndStarted()
    {
        if (!_isAdministrator())
        {
            return DriverLifecycleResult.Failure("SerialMonitor 驱动操作需要管理员权限。请以管理员身份启动 FlexComDotnet。");
        }

        var serviceExists = DriverServiceExists();
        var package = FindDriverPackage();

        if (serviceExists && package is not null && DriverNeedsUpdate(package.SysPath))
        {
            return DriverLifecycleResult.Failure("检测到新版驱动。请在停止业务通信并卸载旧过滤器后部署新版驱动；检测和启动监控不会自动停止或替换正在使用的驱动。");
        }

        if (!serviceExists)
        {
            if (package is null)
            {
                return DriverLifecycleResult.Failure("未找到 SerialMonitor 驱动包（serial_monitor.inf / serial_monitor.sys）。请先构建驱动。");
            }

            var install = InstallDriverCore(package);
            if (!install.Succeeded)
            {
                return install;
            }
        }

        return StartDriverCore();
    }

    /// <summary>
    /// 按需停止驱动服务。
    /// </summary>
    public DriverLifecycleResult StopDriver()
    {
        if (!_isAdministrator())
        {
            return DriverLifecycleResult.Failure("SerialMonitor 驱动操作需要管理员权限。");
        }

        return StopDriverCore();
    }

    /// <summary>
    /// Registers only the selected device for its next enumeration.
    /// Does not restart the port or interrupt an application's active handle.
    /// </summary>
    public DriverLifecycleResult ReenumeratePort(string portName)
    {
        if (!_isAdministrator())
        {
            return DriverLifecycleResult.Failure("重新枚举串口需要管理员权限。");
        }

        var instanceId = _resolvePnpInstanceId(portName);
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return DriverLifecycleResult.Failure($"无法找到 {portName} 对应的 PnP 设备实例。请重新插拔或重启虚拟机。");
        }

        var registerFilter = _pnpFilterRegistration.EnsureRegistered(ServiceName, instanceId);
        if (!registerFilter.Succeeded)
        {
            return registerFilter;
        }

        return DriverLifecycleResult.Success(
            $"已登记：{portName} -> {instanceId}。不会自动重启串口。请在停止业务通信后重新插拔设备或重启系统，随后启动监控。");
    }

    /// <summary>
    /// 内部卸载接口：停止并删除驱动服务。
    /// </summary>
    public DriverLifecycleResult UninstallDriver()
    {
        if (!_isAdministrator())
        {
            return DriverLifecycleResult.Failure("SerialMonitor 驱动操作需要管理员权限。");
        }

        var removeRegistration = _pnpFilterRegistration.RemoveRegistration(ServiceName);
        if (!removeRegistration.Succeeded) return removeRegistration;

        var stop = StopDriverCore();
        if (!stop.Succeeded)
        {
            return DriverLifecycleResult.Failure($"过滤器登记已移除。请在业务停止后重新枚举相关端口或重启系统，再卸载驱动。{stop.Message}");
        }

        var delete = DeleteDriverCore();
        if (!delete.Succeeded)
        {
            return delete;
        }

        return delete;
    }

    private bool DriverServiceExists() =>
        _runCommand("sc", ["query", ServiceName]).ExitCode == 0;

    private DriverLifecycleResult InstallDriverCore(DriverPackage package)
    {
        var pnputil = _runCommand("pnputil", ["/add-driver", package.InfPath, "/install"]);
        if (pnputil.ExitCode == 0 && DriverServiceExists())
        {
            return DriverLifecycleResult.Success("SerialMonitor 驱动已通过 pnputil 安装。");
        }

        var sc = _runCommand("sc", [
            "create", ServiceName,
            "type=", "kernel",
            "start=", "demand",
            "binPath=", package.SysPath,
            "DisplayName=", DisplayName
        ]);

        return sc.ExitCode == 0
            ? DriverLifecycleResult.Success("SerialMonitor 驱动已通过 sc create 安装。")
            : DriverLifecycleResult.Failure($"SerialMonitor 驱动安装失败：{MergeOutput(pnputil)} {MergeOutput(sc)}".Trim());
    }

    private DriverLifecycleResult StartDriverCore()
    {
        var result = _runCommand("sc", ["start", ServiceName]);
        return result.ExitCode == 0 || IsServiceAlreadyRunning(result)
            ? DriverLifecycleResult.Success("SerialMonitor 驱动已启动。")
            : DriverLifecycleResult.Failure($"SerialMonitor 驱动启动失败：{MergeOutput(result)}");
    }

    private DriverLifecycleResult StopDriverCore()
    {
        var result = _runCommand("sc", ["stop", ServiceName]);
        return result.ExitCode == 0 || IsServiceAlreadyStoppedOrMissing(result)
            ? DriverLifecycleResult.Success("SerialMonitor 驱动已停止。")
            : DriverLifecycleResult.Failure($"SerialMonitor 驱动停止失败：{MergeOutput(result)}");
    }

    private DriverLifecycleResult DeleteDriverCore()
    {
        var result = _runCommand("sc", ["delete", ServiceName]);
        return result.ExitCode == 0 || IsServiceAlreadyStoppedOrMissing(result)
            ? DriverLifecycleResult.Success("SerialMonitor 驱动已卸载。")
            : DriverLifecycleResult.Failure($"SerialMonitor 驱动卸载失败：{MergeOutput(result)}");
    }

    private bool DriverNeedsUpdate(string sourceSysPath)
    {
        var installedPath = GetInstalledDriverPath();
        if (installedPath is null || !File.Exists(installedPath))
        {
            return false;
        }

        var sourceVersion = FileVersionInfo.GetVersionInfo(sourceSysPath).FileVersion;
        var installedVersion = FileVersionInfo.GetVersionInfo(installedPath).FileVersion;
        if (!string.IsNullOrWhiteSpace(sourceVersion) && !string.IsNullOrWhiteSpace(installedVersion))
        {
            return !StringComparer.OrdinalIgnoreCase.Equals(sourceVersion, installedVersion);
        }

        var source = new FileInfo(sourceSysPath);
        var installed = new FileInfo(installedPath);
        return source.Length != installed.Length || source.LastWriteTimeUtc > installed.LastWriteTimeUtc.AddSeconds(1);
    }

    private string? GetInstalledDriverPath()
    {
        var result = _runCommand("sc", ["qc", ServiceName]);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var line = MergeOutput(result)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(static x => x.Contains("BINARY_PATH_NAME", StringComparison.OrdinalIgnoreCase));
        var path = line?.Split(':', 2).ElementAtOrDefault(1)?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(windows, path[@"\SystemRoot\".Length..]);
        }

        if (path.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(windows, path);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private DriverPackage? FindDriverPackage()
    {
        foreach (var directory in CandidatePackageDirectories().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var infPath = Path.Combine(directory, "serial_monitor.inf");
            var sysPath = Path.Combine(directory, "serial_monitor.sys");
            if (File.Exists(infPath) && File.Exists(sysPath))
            {
                return new DriverPackage(infPath, sysPath);
            }
        }

        return null;
    }

    private IEnumerable<string> CandidatePackageDirectories()
    {
        if (!string.IsNullOrWhiteSpace(_packageDirectory))
        {
            yield return _packageDirectory;
        }

        yield return Path.Combine(AppContext.BaseDirectory, "driver", "serial-monitor");
        yield return AppContext.BaseDirectory;

        foreach (var parent in EnumerateParents(AppContext.BaseDirectory).Concat(EnumerateParents(Directory.GetCurrentDirectory())))
        {
            yield return Path.Combine(parent, "driver", "serial-monitor", "target", "release", "package");
            yield return Path.Combine(parent, "driver", "serial-monitor", "target", "debug", "package");
            yield return Path.Combine(parent, "driver", "serial-monitor");
        }
    }

    private static IEnumerable<string> EnumerateParents(string path)
    {
        for (var dir = new DirectoryInfo(path); dir is not null; dir = dir.Parent)
        {
            yield return dir.FullName;
        }
    }

    private static bool IsCurrentProcessAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static string? ResolvePnpInstanceId(string portName) =>
        SerialMonitorPortIdentity.ResolveInstanceId(portName);

    private static DriverCommandResult RunCommand(string fileName, IReadOnlyList<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        if (!process.WaitForExit(CommandTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            return new DriverCommandResult(-1, "", $"{fileName} 执行超时。");
        }

        return new DriverCommandResult(process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
    }

    private static string MergeOutput(DriverCommandResult result) =>
        $"{result.StandardOutput} {result.StandardError}".Trim();

    private static string FormatDiagnosticOutput(DriverCommandResult result)
    {
        var output = string.Join(' ', MergeOutput(result)
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(output)
            ? "（无输出）"
            : output.Length <= 500 ? output : $"{output[..500]}…";
    }

    private static bool IsServiceAlreadyRunning(DriverCommandResult result) =>
        ContainsAny(result, "1056", "already been started", "已启动", "已在运行");

    private static bool IsServiceAlreadyStoppedOrMissing(DriverCommandResult result) =>
        ContainsAny(result, "1060", "1062", "does not exist", "has not been started", "未启动", "不存在");

    private static bool ContainsAny(DriverCommandResult result, params string[] values)
    {
        var output = MergeOutput(result);
        return values.Any(value => output.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record DriverPackage(string InfPath, string SysPath);
}

internal interface IPnpFilterRegistration
{
    DriverLifecycleResult EnsureRegistered(string serviceName, string deviceInstanceId);

    DriverLifecycleResult RemoveRegistration(string serviceName);
}

internal sealed class NoopPnpFilterRegistration : IPnpFilterRegistration
{
    public static NoopPnpFilterRegistration Instance { get; } = new();

    public DriverLifecycleResult EnsureRegistered(string serviceName, string deviceInstanceId) =>
        DriverLifecycleResult.Success("SerialMonitor PnP 过滤器注册已跳过。");

    public DriverLifecycleResult RemoveRegistration(string serviceName) =>
        DriverLifecycleResult.Success("SerialMonitor PnP 过滤器注册已跳过。");
}

/// <summary>
/// Registers SerialMonitor as an upper filter on one serial device instance.
/// This keeps unrelated Ports-class devices out of the filter stack while the
/// selected COM device is re-enumerated through DriverEntry.AddDevice.
/// </summary>
internal sealed class DeviceInstanceUpperFilterRegistration : IPnpFilterRegistration
{
    private const string EnumKey = @"SYSTEM\CurrentControlSet\Enum";
    private const string PortsClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4D36E978-E325-11CE-BFC1-08002BE10318}";
    private const string UpperFiltersValue = "UpperFilters";

    public DriverLifecycleResult EnsureRegistered(string serviceName, string deviceInstanceId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceInstanceId))
            {
                return DriverLifecycleResult.Failure("PnP 设备实例不能为空。无法注册串口过滤器。");
            }

            // Migration from earlier builds: remove the class-wide registration
            // before registering the selected device, otherwise every Ports
            // device would continue receiving this filter.
            using (var classKey = Registry.LocalMachine.OpenSubKey(PortsClassKey, writable: true))
            {
                if (classKey is not null)
                {
                    RemoveFilter(classKey, serviceName);
                }
            }

            using var key = Registry.LocalMachine.OpenSubKey($"{EnumKey}\\{deviceInstanceId}", writable: true);
            if (key is null)
            {
                return DriverLifecycleResult.Failure($"无法打开 PnP 设备实例 {deviceInstanceId} 以注册 SerialMonitor 过滤器。");
            }

            var filters = ReadFilters(key);
            if (!filters.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
            {
                key.SetValue(UpperFiltersValue, filters.Append(serviceName).ToArray(), RegistryValueKind.MultiString);
            }

            return DriverLifecycleResult.Success($"SerialMonitor 已注册到 PnP 实例 {deviceInstanceId}；下次枚举设备时生效。");
        }
        catch (UnauthorizedAccessException)
        {
            return DriverLifecycleResult.Failure("注册 SerialMonitor PnP 过滤器需要管理员权限。");
        }
        catch (Exception ex)
        {
            return DriverLifecycleResult.Failure($"注册 SerialMonitor PnP 过滤器失败：{ex.Message}");
        }
    }

    public DriverLifecycleResult RemoveRegistration(string serviceName)
    {
        try
        {
            using (var classKey = Registry.LocalMachine.OpenSubKey(PortsClassKey, writable: true))
            {
                if (classKey is not null)
                {
                    RemoveFilter(classKey, serviceName);
                }
            }

            foreach (var instanceId in FindPortsDeviceInstanceIds())
            {
                using var deviceKey = Registry.LocalMachine.OpenSubKey($"{EnumKey}\\{instanceId}", writable: true);
                if (deviceKey is not null)
                {
                    RemoveFilter(deviceKey, serviceName);
                }
            }

            return DriverLifecycleResult.Success("SerialMonitor PnP 过滤器注册已移除；重新枚举目标串口后完全卸载。");
        }
        catch (Exception ex)
        {
            return DriverLifecycleResult.Failure($"移除 SerialMonitor PnP 过滤器注册失败：{ex.Message}");
        }
    }

    private static string[] ReadFilters(RegistryKey key) =>
        key.GetValue(UpperFiltersValue) switch
        {
            string[] values => values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
            string value when !string.IsNullOrWhiteSpace(value) => [value],
            _ => []
        };

    private static void RemoveFilter(RegistryKey key, string serviceName)
    {
        var filters = ReadFilters(key)
            .Where(filter => !StringComparer.OrdinalIgnoreCase.Equals(filter, serviceName))
            .ToArray();
        if (filters.Length == 0)
        {
            key.DeleteValue(UpperFiltersValue, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(UpperFiltersValue, filters, RegistryValueKind.MultiString);
        }
    }

    private static IEnumerable<string> FindPortsDeviceInstanceIds()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT PNPDeviceID FROM Win32_PnPEntity " +
            "WHERE ClassGuid='{4D36E978-E325-11CE-BFC1-08002BE10318}'");
        foreach (var device in searcher.Get())
        {
            var instanceId = device["PNPDeviceID"]?.ToString();
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                yield return instanceId;
            }
        }
    }
}

internal readonly record struct DriverCommandResult(int ExitCode, string StandardOutput, string StandardError);
