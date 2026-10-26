using FlexComDotnet.Core.Features.ProcessMonitor.ViewModels;
using FlexComDotnet.Core.Features.UsbCapture.ViewModels;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Capture;

public class IndependentCaptureTests
{
    [Fact]
    public void Configuration_ShouldRoundTripIndependentPreferencesWithoutPersistingSessions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"capture-config-{Guid.NewGuid()}.json");
        try
        {
            var store = new JsonConfigurationService(path);
            var config = store.Load();
            config.ProcessMonitor.ExecutablePath = "C:\\Tools\\serial.exe";
            config.ProcessMonitor.Arguments = "--port COM12";
            config.ProcessMonitor.PortFilter = "COM12";
            config.UsbCapture.DeviceFilter = "VID_1234";
            store.Save(config).Should().BeTrue();

            var restored = store.Load();
            restored.ProcessMonitor.ExecutablePath.Should().Be("C:\\Tools\\serial.exe");
            restored.ProcessMonitor.Arguments.Should().Be("--port COM12");
            restored.ProcessMonitor.PortFilter.Should().Be("COM12");
            restored.UsbCapture.DeviceFilter.Should().Be("VID_1234");
            var process = new ProcessMonitorViewModel(store);
            var usb = new UsbCaptureViewModel(store);
            process.OperationStatus.Should().NotBeNullOrWhiteSpace();
            usb.OperationStatus.Should().NotBeNullOrWhiteSpace();
            process.IsMonitoring.Should().BeFalse();
            usb.IsCapturing.Should().BeFalse();
            process.PortFilter = "COM14";
            process.SaveConfigurationCommand.Execute(null);
            store.Load().UsbCapture.DeviceFilter.Should().Be("VID_1234");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
