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
        var installed = false;
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
            if (file == "pnputil")
            {
                installed = true;
                return new DriverCommandResult(0, "", "");
            }

            return file == "sc" && args.SequenceEqual(["query", DriverLifecycleService.ServiceName])
                ? installed ? new DriverCommandResult(0, "", "") : new DriverCommandResult(1060, "", "service missing")
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
    public void EnsureInstalledAndStarted_WhenPnputilStagesPackageButServiceIsStillMissing_ShouldFallbackToScCreate()
    {
        var commands = new List<string>();
        var service = new DriverLifecycleService(_packageDir, Run, () => true);

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeTrue();
        commands.Should().Contain($"pnputil /add-driver {Path.Combine(_packageDir, "serial_monitor.inf")} /install");
        commands.Should().Contain(command => command.StartsWith("sc create SerialMonitor", StringComparison.Ordinal));
        commands.Should().Contain("sc start SerialMonitor");
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
    public void EnsureInstalledAndStarted_WhenInstalledDriverDiffers_ShouldNotReplaceLiveDriver()
    {
        var installedSys = Path.Combine(_packageDir, "installed.sys");
        File.WriteAllText(installedSys, "old driver");

        var commands = new List<string>();
        var service = new DriverLifecycleService(_packageDir, Run, () => true);

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("不会自动停止");
        commands.Should().NotContain("sc stop SerialMonitor");
        commands.Should().NotContain("sc delete SerialMonitor");
        commands.Should().NotContain(c => c.StartsWith("pnputil"));
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

    [Fact]
    public void ReenumeratePort_WhenPnpInstanceIsResolved_ShouldNeverRestartLiveDevice()
    {
        var commands = new List<string>();
        var service = new DriverLifecycleService(
            _packageDir,
            (file, args) =>
            {
                commands.Add($"{file} {string.Join(' ', args)}");
                return new DriverCommandResult(0, "", "");
            },
            () => true,
            NoopPnpFilterRegistration.Instance,
            _ => "ROOT\\VSPD7\\0000");

        var result = service.ReenumeratePort("COM10");

        result.Succeeded.Should().BeTrue();
        commands.Should().NotContain(c => c.Contains("/restart-device"));
        result.Message.Should().Contain("不会自动重启");
    }

    [Fact]
    public void ReenumeratePort_WhenPnpInstanceIsResolved_ShouldRegisterOnlyThatDeviceBeforeRestartingIt()
    {
        var registration = new RecordingPnpFilterRegistration();
        var service = new DriverLifecycleService(
            _packageDir,
            (_, _) => new DriverCommandResult(0, "", ""),
            () => true,
            registration,
            _ => "ROOT\\VSPD7\\0000");

        var result = service.ReenumeratePort("COM10");

        result.Succeeded.Should().BeTrue();
        registration.Registrations.Should().ContainSingle()
            .Which.Should().Be((DriverLifecycleService.ServiceName, "ROOT\\VSPD7\\0000"));
    }

    [Fact]
    public void EnsureInstalledAndStarted_ShouldNotRegisterAClassWidePnpFilter()
    {
        var registration = new RecordingPnpFilterRegistration();
        var service = new DriverLifecycleService(
            _packageDir,
            (_, _) => new DriverCommandResult(0, "", ""),
            () => true,
            registration,
            _ => "ROOT\\VSPD7\\0000");

        var result = service.EnsureInstalledAndStarted();

        result.Succeeded.Should().BeTrue();
        registration.Registrations.Should().BeEmpty();
    }

    [Fact]
    public void ReenumeratePort_ShouldExplainWhenRegistrationTakesEffect()
    {
        var service = new DriverLifecycleService(
            _packageDir,
            (file, args) => file == "pnputil"
                ? new DriverCommandResult(0, "Device restarted successfully.", "")
                : new DriverCommandResult(0, "", ""),
            () => true,
            NoopPnpFilterRegistration.Instance,
            _ => "ROOT\\VSPD7\\0000");

        var result = service.ReenumeratePort("COM10");

        result.Succeeded.Should().BeTrue();
        result.Message.Should().Contain("COM10 -> ROOT\\VSPD7\\0000");
        result.Message.Should().Contain("重新插拔");
        result.Message.Should().Contain("停止业务通信");
    }

    public void Dispose()
    {
        if (Directory.Exists(_packageDir))
        {
            Directory.Delete(_packageDir, recursive: true);
        }
    }

    private sealed class RecordingPnpFilterRegistration : IPnpFilterRegistration
    {
        public List<(string ServiceName, string InstanceId)> Registrations { get; } = [];

        public DriverLifecycleResult EnsureRegistered(string serviceName, string deviceInstanceId)
        {
            Registrations.Add((serviceName, deviceInstanceId));
            return DriverLifecycleResult.Success("registered");
        }

        public DriverLifecycleResult RemoveRegistration(string serviceName) =>
            DriverLifecycleResult.Success("removed");
    }
}
