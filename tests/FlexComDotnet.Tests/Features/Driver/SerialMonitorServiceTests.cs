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
    public void TryReadData_WhenPayloadIsTruncated_ShouldRejectRecord()
    {
        var service = CreateService();
        service.StartMonitoring(@"\Device\Serial0");
        _device.EnqueueData(123, SerialMonitorDataDirection.Rx, [1, 2, 3]);
        _device.BytesReturnedOverride = SerialMonitorInterop.SizeOf<SerialMonitorInterop.CapturedDataHeader>() + 1;
        service.TryReadData(out _).Should().BeFalse();
    }

    [Fact]
    public void Dispose_WhenMonitoring_ShouldStopTheKernelSession()
    {
        var service = CreateService();
        service.StartMonitoring(@"\Device\Serial0");
        service.Dispose();
        service.IsMonitoring.Should().BeFalse();
        _device.Calls.Should().Contain(c => c.Ioctl == SerialMonitorInterop.IoctlStopMonitor);
    }

    [Fact]
    public void InteropContract_ShouldMatchRustDriver()
    {
        SerialMonitorInterop.IoctlStartMonitor.Should().Be(0x8000E040);
        SerialMonitorInterop.IoctlStopMonitor.Should().Be(0x8000E044);
        SerialMonitorInterop.IoctlGetData.Should().Be(0x80006048);
        SerialMonitorInterop.IoctlGetStatus.Should().Be(0x8000604C);
        SerialMonitorInterop.IoctlGetDiagnostics.Should().Be(0x80006050);

        SerialMonitorInterop.SizeOf<SerialMonitorInterop.StartMonitorRequest>().Should().Be(128);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.CapturedDataHeader>().Should().Be(64);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.GetDataResponse>().Should().Be(4160);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverStatus>().Should().Be(64);
        SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverDiagnostics>().Should().Be(20);
        System.Runtime.InteropServices.Marshal.OffsetOf<SerialMonitorInterop.CapturedDataHeader>("Status").ToInt32().Should().Be(32);
        System.Runtime.InteropServices.Marshal.OffsetOf<SerialMonitorInterop.CapturedDataHeader>("DataLength").ToInt32().Should().Be(48);
        System.Runtime.InteropServices.Marshal.OffsetOf<SerialMonitorInterop.CapturedDataHeader>("Direction").ToInt32().Should().Be(56);
    }

    [Fact]
    public void StartMonitoring_WhenNotAdministrator_ShouldNotTouchDriver()
    {
        var service = CreateService(isAdministrator: false);

        var result = service.StartMonitoring(@"\Device\Serial0");

        result.Succeeded.Should().BeFalse();
        _commands.Should().BeEmpty();
        _device.Calls.Should().BeEmpty();
    }

    [Fact]
    public void StartMonitoring_ShouldInstallOpenAndSendStartIoctl()
    {
        var service = CreateService();

        var result = service.StartMonitoring(@"\Device\Serial0");

        result.Succeeded.Should().BeTrue();
        _commands.Should().Contain("sc query SerialMonitor");
        _commands.Should().Contain("sc start SerialMonitor");
        _device.OpenCount.Should().Be(1);
        _device.Calls.Should().ContainSingle(c => c.Ioctl == SerialMonitorInterop.IoctlStartMonitor);
        SerialMonitorInterop.Deserialize<SerialMonitorInterop.StartMonitorRequest>(_device.Calls[0].Input!)
            .PortNameString()
            .Should()
            .Be(@"\Device\Serial0");
    }

    [Fact]
    public void StartMonitoring_ShouldResolveComPortBeforeSendingStartIoctl()
    {
        var service = CreateService(resolvePortDevicePath: _ => @"\Device\USBSER000");

        var result = service.StartMonitoring("COM10");

        result.Succeeded.Should().BeTrue();
        SerialMonitorInterop.Deserialize<SerialMonitorInterop.StartMonitorRequest>(_device.Calls[0].Input!)
            .PortNameString()
            .Should()
            .Be(@"\Device\USBSER000");
    }

    [Fact]
    public void StartMonitoring_WhenDriverRejectsAttach_ShouldExposeWin32AndDriverDiagnostics()
    {
        _device.StartFailureError = 31;
        _device.Diagnostics = new SerialMonitorInterop.DriverDiagnostics
        {
            LastStartStage = 2,
            LastStartStatus = unchecked((int)0xC0000001),
            PnpFilterCount = 0,
            FilterCreateCount = 0,
            LastFilterCreateStatus = 0
        };
        var service = CreateService();

        var result = service.StartMonitoring(@"\Device\Serial0");

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Win32Error=31");
        result.Message.Should().Contain("附加目标串口");
        result.Message.Should().Contain("0xC0000001");
    }

    [Fact]
    public void StopMonitoring_ShouldSendStopIoctl()
    {
        var service = CreateService();
        service.StartMonitoring(@"\Device\Serial0");

        var result = service.StopMonitoring();

        result.Succeeded.Should().BeTrue();
        _device.Calls.Should().Contain(c => c.Ioctl == SerialMonitorInterop.IoctlStopMonitor);
    }

    [Fact]
    public void GetStatus_ShouldParseDriverStatus()
    {
        _device.Status = new SerialMonitorInterop.DriverStatus
        {
            Version = 2,
            State = (byte)SerialMonitorState.Running,
            CapturedEntryCount = 2,
            TotalBytesRx = 10,
            TotalBytesTx = 5
        };
        var service = CreateService();
        service.StartMonitoring(@"\Device\Serial0");

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
        service.StartMonitoring(@"\Device\Serial0");
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
    public void TryReadData_ShouldPreserveFailureAndConfigurationMetadata()
    {
        using var service = CreateService();
        service.StartMonitoring(@"\Device\Serial0");
        var failed = SerialMonitorInterop.GetDataResponse.From(123, SerialMonitorDataDirection.Tx, []);
        failed.Header.Status = unchecked((int)0xC0000120);
        failed.Header.RequestedLength = 9000;
        failed.Header.Sequence = 7;
        failed.Header.RequestId = 42;
        failed.Header.PortId = 3;
        _device.EnqueueResponse(failed);
        service.TryReadData(out var record).Should().BeTrue();
        record!.Status.Should().Be(unchecked((int)0xC0000120));
        record.RequestedLength.Should().Be(9000);
        record.RequestId.Should().Be(42);
        record.PortId.Should().Be(3);
        record.Sequence.Should().Be(7);
        record.Data.Should().BeEmpty();

        var configuration = SerialMonitorInterop.GetDataResponse.From(124, SerialMonitorDataDirection.Control, [0x00, 0xC2, 0x01, 0]);
        configuration.Header.Kind = 1;
        configuration.Header.Ioctl = 0x1B0004;
        _device.EnqueueResponse(configuration);
        service.TryReadData(out record).Should().BeTrue();
        record!.Kind.Should().Be(1);
        record.Ioctl.Should().Be(0x1B0004);
        record.Data.Should().Equal(0x00, 0xC2, 0x01, 0x00);
    }

    [Fact]
    public async Task StartMonitoring_ShouldReadDriverDataAsynchronously()
    {
        var service = CreateService(startReadLoopOnStart: true);
        var received = new TaskCompletionSource<SerialMonitorDataRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DataReceived += (_, record) => received.TrySetResult(record);

        service.StartMonitoring(@"\Device\Serial0");
        _device.EnqueueData(789, SerialMonitorDataDirection.Rx, [0xAA]);

        var record = await received.Task.WaitAsync(TimeSpan.FromSeconds(1));

        record.Direction.Should().Be(SerialMonitorDataDirection.Rx);
        record.Data.Should().Equal(0xAA);
    }

    private SerialMonitorService CreateService(
        bool isAdministrator = true,
        bool startReadLoopOnStart = false,
        Func<string, string>? resolvePortDevicePath = null)
    {
        var lifecycle = new DriverLifecycleService(_packageDir, RunCommand, () => isAdministrator);
        return new SerialMonitorService(
            lifecycle,
            () => _device,
            () => isAdministrator,
            startReadLoopOnStart,
            resolvePortDevicePath);
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
        private readonly System.Collections.Concurrent.ConcurrentQueue<SerialMonitorInterop.GetDataResponse> _data = new();

        public List<(uint Ioctl, byte[]? Input)> Calls { get; } = [];

        public int OpenCount { get; private set; }

        public bool IsOpen { get; private set; }

        public int LastError { get; private set; }

        public int StartFailureError { get; set; }
        public int? BytesReturnedOverride { get; set; }

        public SerialMonitorInterop.DriverStatus Status { get; set; }

        public SerialMonitorInterop.DriverDiagnostics Diagnostics { get; set; }

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

            if (ioctl == SerialMonitorInterop.IoctlGetDiagnostics)
            {
                SerialMonitorInterop.WriteStruct(Diagnostics, output);
                bytesReturned = SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverDiagnostics>();
                return true;
            }

            if (ioctl == SerialMonitorInterop.IoctlStartMonitor && StartFailureError != 0)
            {
                LastError = StartFailureError;
                return false;
            }

            if (ioctl == SerialMonitorInterop.IoctlGetData && _data.TryDequeue(out var response))
            {
                SerialMonitorInterop.WriteStruct(response, output);
                bytesReturned = BytesReturnedOverride ?? SerialMonitorInterop.SizeOf<SerialMonitorInterop.GetDataResponse>();
                return true;
            }

            var succeeded = ioctl is SerialMonitorInterop.IoctlStartMonitor or SerialMonitorInterop.IoctlStopMonitor;
            LastError = succeeded ? 0 : 1;
            return succeeded;
        }

        public void EnqueueData(ulong timestamp, SerialMonitorDataDirection direction, byte[] payload)
        {
            var response = SerialMonitorInterop.GetDataResponse.From(timestamp, direction, payload);
            _data.Enqueue(response);
        }

        public void EnqueueResponse(SerialMonitorInterop.GetDataResponse response) => _data.Enqueue(response);

        public void Dispose() => IsOpen = false;
    }
}
