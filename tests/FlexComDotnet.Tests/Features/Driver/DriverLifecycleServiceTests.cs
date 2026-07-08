using FlexComDotnet.Core.Features.Driver.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Driver;

public class DriverLifecycleServiceTests : IDisposable
{
    private readonly string _packageDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public DriverLifecycleServiceTests()
    {
        Directory.CreateDirectory(_packageDir);
        File.WriteAllText(Path.Combine(_packageDir, "serial_monitor.inf"), "inf");
        File.WriteAllText(Path.Combine(_packageDir, "serial_monitor.sys"), "sys");
    }

    [Fact]
    public void EnsureInstalledAndStarted_WhenNotAdministrator_ShouldNotRunCommands()
    {
        var commands = new List<string>();
        var service = new DriverLifecycleService(
            _packageDir,
            (file, args) =>
            {
                commands.Add($"{file} {string.Join(' ', args)}");
                return new DriverCommandResult(0, "", "");
            },
            () => false);

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeFalse();
        commands.Should().BeEmpty();
    }

    [Fact]
    public void EnsureInstalledAndStarted_WhenServiceMissing_ShouldInstallWithPnputilAndStart()
    {
        var commands = new List<string>();
        var service = new DriverLifecycleService(_packageDir, Run, () => true);

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeTrue();
        commands.Should().Contain("sc query SerialMonitor");
        commands.Should().Contain($"pnputil /add-driver {Path.Combine(_packageDir, "serial_monitor.inf")} /install");
        commands.Should().Contain("sc start SerialMonitor");
        commands.Should().NotContain(command => command.StartsWith("sc create", StringComparison.Ordinal));
        return;

        DriverCommandResult Run(string file, IReadOnlyList<string> args)
        {
            commands.Add($"{file} {string.Join(' ', args)}");
            return file == "sc" && args.SequenceEqual(["query", DriverLifecycleService.ServiceName])
                ? new DriverCommandResult(1060, "", "service missing")
                : new DriverCommandResult(0, "", "");
        }
    }

    [Fact]
    public void EnsureInstalledAndStarted_WhenPnputilFails_ShouldFallbackToScCreate()
    {
        var commands = new List<string>();
        var service = new DriverLifecycleService(_packageDir, Run, () => true);

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeTrue();
        commands.Should().Contain(command => command.StartsWith("sc create SerialMonitor", StringComparison.Ordinal));
        commands.Should().Contain("sc start SerialMonitor");
        return;

        DriverCommandResult Run(string file, IReadOnlyList<string> args)
        {
            commands.Add($"{file} {string.Join(' ', args)}");
            if (file == "sc" && args.SequenceEqual(["query", DriverLifecycleService.ServiceName]))
            {
                return new DriverCommandResult(1060, "", "service missing");
            }

            return file == "pnputil"
                ? new DriverCommandResult(1, "", "pnputil failed")
                : new DriverCommandResult(0, "", "");
        }
    }

    [Fact]
    public void EnsureInstalledAndStarted_WhenInstalledDriverDiffers_ShouldStopDeleteReinstallAndStart()
    {
        var installedSys = Path.Combine(_packageDir, "installed.sys");
        File.WriteAllText(installedSys, "old driver");

        var commands = new List<string>();
        var service = new DriverLifecycleService(_packageDir, Run, () => true);

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeTrue();
        commands.Should().Contain("sc stop SerialMonitor");
        commands.Should().Contain("sc delete SerialMonitor");
        commands.Should().Contain($"pnputil /add-driver {Path.Combine(_packageDir, "serial_monitor.inf")} /install");
        commands.Should().Contain("sc start SerialMonitor");
        return;

        DriverCommandResult Run(string file, IReadOnlyList<string> args)
        {
            commands.Add($"{file} {string.Join(' ', args)}");
            if (file == "sc" && args.SequenceEqual(["qc", DriverLifecycleService.ServiceName]))
            {
                return new DriverCommandResult(0, $"BINARY_PATH_NAME   : {installedSys}", "");
            }

            return new DriverCommandResult(0, "", "");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_packageDir))
        {
            Directory.Delete(_packageDir, recursive: true);
        }
    }
}
