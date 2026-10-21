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
    public void ControlRecord_ShouldDecodeBaudRateRegardlessOfHexModeAndKeepRawData()
    {
        using var vm = CreateViewModel();
        byte[] bytes = [0x00, 0xC2, 0x01, 0x00];
        vm.AddRecord(new(DateTime.Today, SerialMonitorDataDirection.Control, bytes)
        { Kind = 1, Ioctl = 0x001B0004, RequestedLength = 4, RequestId = 17 });

        var row = vm.FilteredRecords.Single();
        row.EventLabel.Should().Be("IOCTL_SERIAL_SET_BAUD_RATE");
        row.Content.Should().Contain("115200").And.Contain("波特率");
        vm.IsHexDisplay = false;
        row.Content.Should().Contain("115200");
        row.Record.Data.Should().Equal(bytes);
        vm.SearchKeyword = "SET_BAUD_RATE";
        vm.FilteredRecords.Should().ContainSingle();
        vm.SearchKeyword = "115200";
        vm.FilteredRecords.Should().ContainSingle();
        vm.SearchKeyword = "00 C2 01 00";
        vm.FilteredRecords.Should().ContainSingle();
    }

    [Theory]
    [InlineData(0x001B000C, new byte[] { 0, 0, 8 }, "数据位=8", "停止位=1", "校验=无")]
    [InlineData(0x001B000C, new byte[] { 2, 2, 7 }, "数据位=7", "停止位=2", "校验=偶")]
    [InlineData(0x001B000C, new byte[] { 99, 99, 8 }, "数据位=8", "未知(99)", "校验=未知")]
    [InlineData(0x001B0064, new byte[] { 9, 0, 0, 0, 129, 0, 0, 0, 244, 1, 0, 0, 100, 0, 0, 0 }, "CTS握手=开", "RTS=握手", "XonLimit=500")]
    [InlineData(0x001B0050, new byte[] { }, "查询波特率", "未采集", "返回")]
    [InlineData(0x001B0024, new byte[] { }, "置位 DTR", "DTR", "置位")]
    [InlineData(0x001B0004, new byte[] { 0x80 }, "设置波特率", "不足", "1/4")]
    [InlineData(0x001B0004, new byte[] { }, "设置波特率", "未采集", "参数")]
    [InlineData(0xDEADBEEF, new byte[] { 0x41, 0xFF }, "未知", "0xDEADBEEF", "41 FF")]
    public void ControlRecord_ShouldDescribeCapturedParametersOrExplicitlyReportMissingData(
        uint ioctl, byte[] data, string expected1, string expected2, string expected3)
    {
        var row = new SerialMonitorRecordViewModel(new(DateTime.Today, SerialMonitorDataDirection.Control, data)
        { Kind = 1, Ioctl = ioctl }, true);
        row.Content.Should().Contain(expected1).And.Contain(expected2).And.Contain(expected3);
    }

    [Fact]
    public void ControlRecord_WhenFailed_ShouldDescribeRequestWithoutClaimingSuccess()
    {
        var row = new SerialMonitorRecordViewModel(new(DateTime.Today, SerialMonitorDataDirection.Control, [0, 0xC2, 1, 0])
        { Kind = 1, Ioctl = 0x001B0004, Status = unchecked((int)0xC000000D) }, true);
        row.Content.Should().Contain("115200").And.NotContain("已设置");
        row.Result.Should().Contain("失败 0xC000000D");
    }

    [Fact]
    public void RawDetails_ShouldPreserveMetadataAndBytesAcrossDisplayChanges()
    {
        var row = new SerialMonitorRecordViewModel(new(DateTime.Today, SerialMonitorDataDirection.Control, [0, 0xC2, 1, 0])
        { Kind = 1, Ioctl = 0x001B0004, PortId = 10, Sequence = 23, RequestId = 17, RequestedLength = 4 }, true);
        string details = row.RawDetails;
        row.SetDisplayMode(false);
        row.RawDetails.Should().Be(details);
        details.Should().Contain("00 C2 01 00").And.Contain("0x001B0004")
            .And.Contain("请求 ID：17").And.Contain("序号：23").And.Contain("端口 ID：10")
            .And.Contain("完成长度：0").And.Contain("采集数据：4 字节");
    }

    [Theory]
    [InlineData(1, 0, "数据不可用")]
    [InlineData(2, 0, "数据分片")]
    [InlineData(0, 4, "数据分片")]
    public void ControlRecord_WhenUnavailableOrFragmented_ShouldNotDecodeParameters(ushort flags, uint offset, string expected)
    {
        var row = new SerialMonitorRecordViewModel(new(DateTime.Today, SerialMonitorDataDirection.Control, [0, 0xC2, 1, 0])
        { Kind = 1, Ioctl = 0x001B0004, Flags = flags, Offset = offset }, true);
        row.Content.Should().Contain(expected).And.NotContain("115200");
    }

    [Theory]
    [InlineData(0x001B0005)]
    [InlineData(0x001B4004)]
    [InlineData(0x001C0004)]
    public void ControlRecord_WhenCodeOnlyPartiallyMatches_ShouldRemainUnknown(uint ioctl)
    {
        var row = new SerialMonitorRecordViewModel(new(DateTime.Today, SerialMonitorDataDirection.Control, [0, 0xC2, 1, 0])
        { Kind = 1, Ioctl = ioctl }, true);
        row.Content.Should().Contain("未知").And.NotContain("115200");
    }

    [Fact]
    public void ExportRecords_WhenControlDecoded_ShouldAlsoPreserveIoctlAndRawHex()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        try
        {
            using var vm = CreateViewModel();
            vm.AddRecord(new(DateTime.Today, SerialMonitorDataDirection.Control, [0, 0xC2, 1, 0])
            { Kind = 1, Ioctl = 0x001B0004 });
            vm.ExportRecords(file);
            File.ReadAllText(file).Should().Contain("Ioctl,RawHex").And.Contain("115200")
                .And.Contain("0x001B0004,00 C2 01 00");
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void FixedFrameLength_ShouldRebuildViewWithoutLosingRawRecords()
    {
        using var vm = CreateViewModel();
        vm.AddRecord(new(DateTime.Today, SerialMonitorDataDirection.Rx, [1, 2]));
        vm.AddRecord(new(DateTime.Today, SerialMonitorDataDirection.Rx, [3, 4, 5, 6]));
        vm.FixedFrameLength = 3;
        vm.FilteredRecords.Select(r => r.Content).Should().Equal("01 02 03", "04 05 06");
        vm.FilteredRecords.Should().OnlyContain(r => r.EventLabel == "定长帧");
        vm.FixedFrameLength = 0;
        vm.FilteredRecords.Select(r => r.Content).Should().Equal("01 02", "03 04 05 06");
    }

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

        File.ReadAllText(file).Should().Contain("2026-01-02 03:04:05.0060000,TX,41");
        File.ReadAllText(file).Should().Contain("RequestId,Event,Status,Requested,Transferred,Offset,Flags");
        File.Delete(file);
    }

    [Fact]
    public void StartMonitoring_WhenDriverRejectsPort_ShouldKeepFailureMessageVisible()
    {
        var monitor = new SerialMonitorService(
            new DriverLifecycleService(null, (_, _) => new DriverCommandResult(0, "", ""), () => true),
            () => new RejectingStartDevice(),
            () => true,
            startReadLoopOnStart: false,
            resolvePortDevicePath: portName => $@"\Device\{portName}");
        var serialPortService = new Mock<ISerialPortService>();
        serialPortService.Setup(s => s.GetAvailablePorts()).Returns([
            new SerialPortInfo { PortName = "COM10" }
        ]);
        using var vm = new SerialMonitorViewModel(monitor, serialPortService.Object);

        vm.StartStopMonitoringCommand.Execute(null);

        vm.IsMonitoring.Should().BeFalse();
        vm.OperationStatus.Should().Contain("启动串口监控失败");
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
        public int LastError => 0;
        public bool Open() => IsOpen = true;
        public bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
        {
            bytesReturned = 0;
            return ioctl is SerialMonitorInterop.IoctlStartMonitor or SerialMonitorInterop.IoctlStopMonitor;
        }

        public void Dispose() => IsOpen = false;
    }

    private sealed class RejectingStartDevice : ISerialMonitorDevice
    {
        public bool IsOpen { get; private set; }
        public int LastError => 31;
        public bool Open() => IsOpen = true;
        public bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
        {
            bytesReturned = 0;
            return ioctl == SerialMonitorInterop.IoctlStopMonitor;
        }

        public void Dispose() => IsOpen = false;
    }
}
