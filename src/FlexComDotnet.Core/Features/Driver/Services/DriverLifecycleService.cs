using System.Diagnostics;
using System.Security.Principal;

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

    public DriverLifecycleService()
        : this(null, RunCommand, IsCurrentProcessAdministrator)
    {
    }

    internal DriverLifecycleService(
        string? packageDirectory,
        Func<string, IReadOnlyList<string>, DriverCommandResult> runCommand,
        Func<bool> isAdministrator)
    {
        _packageDirectory = packageDirectory;
        _runCommand = runCommand;
        _isAdministrator = isAdministrator;
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
            StopDriverCore();
            var delete = DeleteDriverCore();
            if (!delete.Succeeded)
            {
                return delete;
            }

            serviceExists = false;
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
    /// 内部卸载接口：停止并删除驱动服务。
    /// </summary>
    public DriverLifecycleResult UninstallDriver()
    {
        if (!_isAdministrator())
        {
            return DriverLifecycleResult.Failure("SerialMonitor 驱动操作需要管理员权限。");
        }

        var stop = StopDriverCore();
        if (!stop.Succeeded)
        {
            return stop;
        }

        return DeleteDriverCore();
    }

    private bool DriverServiceExists() =>
        _runCommand("sc", ["query", ServiceName]).ExitCode == 0;

    private DriverLifecycleResult InstallDriverCore(DriverPackage package)
    {
        var pnputil = _runCommand("pnputil", ["/add-driver", package.InfPath, "/install"]);
        if (pnputil.ExitCode == 0)
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

internal readonly record struct DriverCommandResult(int ExitCode, string StandardOutput, string StandardError);
