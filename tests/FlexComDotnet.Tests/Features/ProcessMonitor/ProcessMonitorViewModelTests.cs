using FlexComDotnet.Core.Features.ProcessMonitor.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Services;
using FlexComDotnet.Core.Features.ProcessMonitor.ViewModels;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Capture.Models;
using FluentAssertions;
using Moq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexComDotnet.Core.Features.Protocol.Models;

namespace FlexComDotnet.Tests.Features.ProcessMonitor;

public class ProcessMonitorViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_WhenPreferencesContainNulls_ShouldUseUsableDefaults(bool missingSection)
    {
        var configuration = new FlexComDotnet.Core.Features.Serial.Models.AppConfig
        {
            ProcessMonitor = missingSection ? null! : new() { ParserName = null!, SearchText = null!, PortFilter = null!, FrameLength = -1 }
        };
        var store = new Mock<IConfigurationService>();
        store.Setup(c => c.Load()).Returns(configuration);
        await using var session = new ProcessMonitorSession(new Mock<IProcessMonitorTransport>().Object);
        var vm = new ProcessMonitorViewModel(store.Object, session, new(), new ProtocolParserService(new ChecksumService()));
        vm.Refresh();
        vm.ParserName.Should().BeEmpty();
        vm.FrameLength.Should().Be(7);
        vm.SaveConfigurationCommand.Execute(null);
        configuration.ProcessMonitor.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_WhenViewMissesEvictedRecords_ShouldBreakPartialFrameAndResumeIncrementally()
    {
        var transport = new Mock<IProcessMonitorTransport>();
        await using var session = new ProcessMonitorSession(transport.Object, 2);
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new FlexComDotnet.Core.Features.Serial.Models.AppConfig());
        var vm = new ProcessMonitorViewModel(config.Object, session, new(), new ProtocolParserService(new ChecksumService()))
            { FramingEnabled = true, FrameLength = 3 };
        await session.AttachAsync(new(123, 987, "test", "x64"));
        void Emit(byte value) => transport.Raise(t => t.RecordReceived += null, transport.Object,
            new ProcessMonitorRecord { SessionId = session.SessionId, ProcessId = 123, ProcessStartTime = 987,
                Sequence = value, Handle = 42, Data = [value], CompletedLength = 1, Succeeded = true });
        Emit(1); vm.Refresh();
        Emit(2); Emit(3); Emit(4); vm.Refresh();
        vm.Frames.Should().BeEmpty("the hidden view lost record 2 and cannot combine across that gap");
        Emit(5); vm.Refresh();
        vm.Frames.Should().ContainSingle().Which.Frame.Data.Should().Equal(3, 4, 5);
    }

    [Fact]
    public async Task Start_WhenExecutableIsMissing_ShouldReportFailureWithoutStartingCapture()
    {
        var transport = new Mock<IProcessMonitorTransport>();
        await using var session = new ProcessMonitorSession(transport.Object);
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new FlexComDotnet.Core.Features.Serial.Models.AppConfig());
        var vm = new ProcessMonitorViewModel(config.Object, session, new(), new ProtocolParserService(new ChecksumService()));
        vm.StartCommand.CanExecute(null).Should().BeFalse();
        vm.ExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        vm.StartCommand.CanExecute(null).Should().BeTrue();
        await vm.StartCommand.ExecuteAsync(null);
        vm.IsMonitoring.Should().BeFalse();
        vm.IsBusy.Should().BeFalse();
        vm.OperationStatus.Should().Contain(Path.GetFileName(vm.ExecutablePath));
        vm.StopCommand.CanExecute(null).Should().BeFalse();
        vm.SelectedTarget = new(Environment.ProcessId, 1, "capture host", "x64");
        vm.AttachCommand.CanExecute(null).Should().BeFalse("the capture host must never inject itself");
    }

    [Fact]
    public async Task AnalysisAndExport_ShouldKeepOriginalRecordsAndParseOnlyWhenEnabled()
    {
        var transport = new Mock<IProcessMonitorTransport>();
        await using var session = new ProcessMonitorSession(transport.Object);
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new FlexComDotnet.Core.Features.Serial.Models.AppConfig());
        var parsers = new ProtocolParserService(new ChecksumService());
        parsers.RegisterDefinition(new() { Name = "Modbus-RTU", ProtocolType = ProtocolType.ModbusRtu });
        var vm = new ProcessMonitorViewModel(config.Object, session, new(), parsers);
        await session.AttachAsync(new(123, 987, "test", "x64"));
        var record = new ProcessMonitorRecord { SessionId = session.SessionId, ProcessId = 123,
            ProcessStartTime = 987, Handle = 42, HandleGeneration = 1, PortName = "COM10", Api = "ReadFile",
            Direction = CaptureDirection.Receive, Succeeded = true, RequestedLength = 16 };
        transport.Raise(t => t.RecordReceived += null, transport.Object, record with { Sequence = 1, CompletedLength = 3, Data = [1, 3, 2] });
        transport.Raise(t => t.RecordReceived += null, transport.Object, record with { Sequence = 2, CompletedLength = 4, Data = [0, 10, 0x38, 0x43] });
        vm.Refresh();
        vm.Frames.Should().BeEmpty();
        vm.FramingEnabled = true;
        vm.FrameLength = 7;
        vm.ParserName = "Modbus-RTU";
        vm.SearchText = "0A 38";
        vm.Refresh();
        vm.Records.Should().ContainSingle().Which.Record.Sequence.Should().Be(2);
        vm.Frames.Should().ContainSingle().Which.Parsed!.IsValid.Should().BeTrue();
        vm.Frames[0].Frame.Records.Select(r => r.RecordId).Should().Equal(1, 2);
        var restored = JsonSerializer.Deserialize<ProcessMonitorRecord[]>(vm.ExportVisibleJson(),
            new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
        restored.Should().ContainSingle().Which.Should().BeEquivalentTo(vm.Records[0].Record);
        parsers.RemoveParser("Modbus-RTU");
        vm.Refresh();
        vm.Frames.Should().ContainSingle().Which.Parsed!.IsValid.Should().BeFalse("removed protocol definitions cannot leave a stale success result");
        vm.PortFilter = "COM1";
        vm.Refresh();
        vm.Records.Should().BeEmpty("COM1 must not match COM10");
        session.Records.Should().HaveCount(2, "display filters cannot remove capture history");
    }

    [Fact]
    public async Task AttachAndRefresh_ShouldShowCompletedBytesAndPreserveSessionAcrossViews()
    {
        var transport = new Mock<IProcessMonitorTransport>();
        await using var session = new ProcessMonitorSession(transport.Object);
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new FlexComDotnet.Core.Features.Serial.Models.AppConfig());
        var parsers = new ProtocolParserService(new ChecksumService());
        var vm = new ProcessMonitorViewModel(config.Object, session, new ProcessMonitorTargets(), parsers);
        vm.SelectedTarget = new(123, 987, "test", "x64");
        await vm.AttachCommand.ExecuteAsync(null);
        transport.Raise(t => t.RecordReceived += null, transport.Object, new ProcessMonitorRecord
        {
            SessionId = session.SessionId, ProcessId = 123, ProcessStartTime = 987,
            Sequence = 1, Api = "ReadFile", PortName = "COM10", Direction = CaptureDirection.Receive,
            RequestedLength = 16, CompletedLength = 2, Succeeded = true, Data = [0x41, 0x42]
        });
        vm.Refresh();
        vm.IsMonitoring.Should().BeTrue();
        vm.Records.Should().ContainSingle().Which.DataText.Should().Be("41 42");
        vm.Records[0].Record.CompletedLength.Should().Be(2);
        var reopened = new ProcessMonitorViewModel(config.Object, session, new ProcessMonitorTargets(), parsers);
        reopened.Refresh();
        reopened.Records.Should().HaveCount(1);
        reopened.IsMonitoring.Should().BeTrue();
        await reopened.StopCommand.ExecuteAsync(null);
        vm.Refresh();
        vm.IsMonitoring.Should().BeFalse();
        vm.Records.Should().HaveCount(1);
    }
}
