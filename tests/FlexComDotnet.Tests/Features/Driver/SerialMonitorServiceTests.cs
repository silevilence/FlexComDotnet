using FlexComDotnet.Core.Features.Driver.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Driver;

public class SerialMonitorServiceTests : IDisposable
{
    private readonly string _packageDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly List<string> _commands = [];
    private readonly FakeSerialMonitorDevice _device = new();

    public SerialMonitorServiceTests()
    {
        Directory.CreateDirectory(_packageDir);
        File.WriteAllText(Path.Combine(_packageDir, "serial_monitor.inf"), "inf");
        File.WriteAllText(Path.Combine(_packageDir, "serial_monitor.sys"), "sys");
    }

    [Fact]
    public void InteropContract_ShouldMatchRustDriver()
    {
        SerialMonitorInterop.IoctlStartMonitor.Should().Be(0x8000E000);
        SerialMonitorInterop.IoctlStopMonitor.Should().Be(0x8000E004);
        SerialMonitorInterop.IoctlGetData.Should().Be(0x80006008);
        SerialMonitorInterop.IoctlGetStatus.Should().Be(0x8000600C);

        SerialMonitorInterop.SizeOf<SerialMonitorInterop.StartMonitorRequest>().Should().Be(128);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.CapturedDataHeader>().Should().Be(16);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.GetDataResponse>().Should().Be(4112);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverStatus>().Should().Be(24);
    }

    [Fact]
    public void StartMonitoring_WhenNotAdministrator_ShouldNotTouchDriver()
    {
        var service = CreateService(isAdministrator: false);

        var result = service.StartMonitoring(@"\\Device\\Serial0");

        result.Succeeded.Should().BeFalse();
        _commands.Should().BeEmpty();
        _device.Calls.Should().BeEmpty();
    }

    [Fact]
    public void StartMonitoring_ShouldInstallOpenAndSendStartIoctl()
    {
        var service = CreateService();

        var result = service.StartMonitoring(@"\\Device\\Serial0");

        result.Succeeded.Should().BeTrue();
        _commands.Should().Contain("sc query SerialMonitor");
        _commands.Should().Contain("sc start SerialMonitor");
        _device.OpenCount.Should().Be(1);
        _device.Calls.Should().ContainSingle(c => c.Ioctl == SerialMonitorInterop.IoctlStartMonitor);
        SerialMonitorInterop.Deserialize<SerialMonitorInterop.StartMonitorRequest>(_device.Calls[0].Input!)
            .PortNameString()
            .Should()
            .Be(@"\\Device\\Serial0");
    }

    [Fact]
    public void StopMonitoring_ShouldSendStopIoctl()
    {
        var service = CreateService();
        service.StartMonitoring(@"\\Device\\Serial0");

        var result = service.StopMonitoring();

        result.Succeeded.Should().BeTrue();
        _device.Calls.Should().Contain(c => c.Ioctl == SerialMonitorInterop.IoctlStopMonitor);
    }

    [Fact]
    public void GetStatus_ShouldParseDriverStatus()
    {
        _device.Status = new SerialMonitorInterop.DriverStatus
        {
            State = (byte)SerialMonitorState.Running,
            CapturedEntryCount = 2,
            TotalBytesRx = 10,
            TotalBytesTx = 5
        };
        var service = CreateService();
        service.StartMonitoring(@"\\Device\\Serial0");

        var status = service.GetStatus();

        status.Should().NotBeNull();
        status!.State.Should().Be(SerialMonitorState.Running);
        status.CapturedEntryCount.Should().Be(2);
        status.TotalBytesRx.Should().Be(10);
        status.TotalBytesTx.Should().Be(5);
    }

    [Fact]
    public void TryReadData_ShouldParseTxAndRxRecords()
    {
        var service = CreateService();
        service.StartMonitoring(@"\\Device\\Serial0");
        _device.EnqueueData(123, SerialMonitorDataDirection.Tx, [0x01, 0x02]);
        _device.EnqueueData(456, SerialMonitorDataDirection.Rx, [0x03]);

        service.TryReadData(out var tx).Should().BeTrue();
        service.TryReadData(out var rx).Should().BeTrue();

        tx!.Direction.Should().Be(SerialMonitorDataDirection.Tx);
        tx.Data.Should().Equal(0x01, 0x02);
        rx!.Direction.Should().Be(SerialMonitorDataDirection.Rx);
        rx.Data.Should().Equal(0x03);
    }

    [Fact]
    public async Task StartMonitoring_ShouldReadDriverDataAsynchronously()
    {
        var service = CreateService(startReadLoopOnStart: true);
        var received = new TaskCompletionSource<SerialMonitorDataRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DataReceived += (_, record) => received.TrySetResult(record);

        service.StartMonitoring(@"\\Device\\Serial0");
        _device.EnqueueData(789, SerialMonitorDataDirection.Rx, [0xAA]);

        var record = await received.Task.WaitAsync(TimeSpan.FromSeconds(1));

        record.Direction.Should().Be(SerialMonitorDataDirection.Rx);
        record.Data.Should().Equal(0xAA);
    }

    private SerialMonitorService CreateService(bool isAdministrator = true, bool startReadLoopOnStart = false)
    {
        var lifecycle = new DriverLifecycleService(_packageDir, RunCommand, () => isAdministrator);
        return new SerialMonitorService(lifecycle, () => _device, () => isAdministrator, startReadLoopOnStart);
    }

    private DriverCommandResult RunCommand(string file, IReadOnlyList<string> args)
    {
        _commands.Add($"{file} {string.Join(' ', args)}");
        return file == "sc" && args.SequenceEqual(["query", DriverLifecycleService.ServiceName])
            ? new DriverCommandResult(0, "", "")
            : new DriverCommandResult(0, "", "");
    }

    public void Dispose()
    {
        _device.Dispose();
        if (Directory.Exists(_packageDir))
        {
            Directory.Delete(_packageDir, recursive: true);
        }
    }

    private sealed class FakeSerialMonitorDevice : ISerialMonitorDevice
    {
        private readonly Queue<SerialMonitorInterop.GetDataResponse> _data = [];

        public List<(uint Ioctl, byte[]? Input)> Calls { get; } = [];

        public int OpenCount { get; private set; }

        public bool IsOpen { get; private set; }

        public SerialMonitorInterop.DriverStatus Status { get; set; }

        public bool Open()
        {
            OpenCount++;
            IsOpen = true;
            return true;
        }

        public bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
        {
            Calls.Add((ioctl, input));
            bytesReturned = 0;

            if (ioctl == SerialMonitorInterop.IoctlGetStatus)
            {
                SerialMonitorInterop.WriteStruct(Status, output);
                bytesReturned = SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverStatus>();
                return true;
            }

            if (ioctl == SerialMonitorInterop.IoctlGetData && _data.TryDequeue(out var response))
            {
                SerialMonitorInterop.WriteStruct(response, output);
                bytesReturned = SerialMonitorInterop.SizeOf<SerialMonitorInterop.GetDataResponse>();
                return true;
            }

            return ioctl is SerialMonitorInterop.IoctlStartMonitor or SerialMonitorInterop.IoctlStopMonitor;
        }

        public void EnqueueData(ulong timestamp, SerialMonitorDataDirection direction, byte[] payload)
        {
            var response = SerialMonitorInterop.GetDataResponse.From(timestamp, direction, payload);
            _data.Enqueue(response);
        }

        public void Dispose() => IsOpen = false;
    }
}
