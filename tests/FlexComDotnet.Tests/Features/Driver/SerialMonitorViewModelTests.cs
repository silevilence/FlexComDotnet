using FlexComDotnet.Core.Features.Driver.Services;
using FlexComDotnet.Core.Features.Driver.ViewModels;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Driver;

public class SerialMonitorViewModelTests
{
    [Fact]
    public void RefreshPortsCommand_ShouldLoadAvailablePortsAndSelectFirst()
    {
        var serialPortService = new Mock<ISerialPortService>();
        serialPortService.Setup(s => s.GetAvailablePorts()).Returns([
            new SerialPortInfo { PortName = "COM1" },
            new SerialPortInfo { PortName = "COM2" }
        ]);
        using var vm = CreateViewModel(serialPortService.Object);

        vm.RefreshPortsCommand.Execute(null);

        vm.Ports.Select(p => p.PortName).Should().Equal("COM1", "COM2");
        vm.SelectedPort!.PortName.Should().Be("COM1");
    }

    [Fact]
    public void AddRecord_ShouldSwitchBetweenHexAndTextDisplay()
    {
        using var vm = CreateViewModel();

        vm.AddRecord(new SerialMonitorDataRecord(DateTime.Today, SerialMonitorDataDirection.Rx, [0x41, 0x42]));
        vm.FilteredRecords[0].Content.Should().Be("41 42");

        vm.IsHexDisplay = false;

        vm.FilteredRecords[0].Content.Should().Be("AB");
    }

    [Fact]
    public void Filters_ShouldApplyDirectionAndKeyword()
    {
        using var vm = CreateViewModel();
        vm.AddRecord(new SerialMonitorDataRecord(DateTime.Today, SerialMonitorDataDirection.Tx, [0x41]));
        vm.AddRecord(new SerialMonitorDataRecord(DateTime.Today, SerialMonitorDataDirection.Rx, [0x42]));

        vm.ShowTx = false;
        vm.SearchKeyword = "42";

        vm.FilteredRecords.Should().ContainSingle();
        vm.FilteredRecords[0].Direction.Should().Be(SerialMonitorDataDirection.Rx);
    }

    [Fact]
    public void ExportRecords_ShouldWriteFilteredRecords()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        using var vm = CreateViewModel();
        vm.AddRecord(new SerialMonitorDataRecord(new DateTime(2026, 1, 2, 3, 4, 5, 6), SerialMonitorDataDirection.Tx, [0x41]));

        vm.ExportRecords(file);

        File.ReadAllText(file).Should().Contain("2026-01-02 03:04:05.006,TX,41");
        File.Delete(file);
    }

    private static SerialMonitorViewModel CreateViewModel(ISerialPortService? serialPortService = null)
    {
        var monitor = new SerialMonitorService(
            new DriverLifecycleService(null, (_, _) => new DriverCommandResult(0, "", ""), () => true),
            () => new FakeSerialMonitorDevice(),
            () => true,
            startReadLoopOnStart: false);
        return new SerialMonitorViewModel(
            monitor,
            serialPortService ?? Mock.Of<ISerialPortService>(s => s.GetAvailablePorts() == Array.Empty<SerialPortInfo>()));
    }

    private sealed class FakeSerialMonitorDevice : ISerialMonitorDevice
    {
        public bool IsOpen { get; private set; }
        public bool Open() => IsOpen = true;
        public bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
        {
            bytesReturned = 0;
            return ioctl is SerialMonitorInterop.IoctlStartMonitor or SerialMonitorInterop.IoctlStopMonitor;
        }

        public void Dispose() => IsOpen = false;
    }
}
