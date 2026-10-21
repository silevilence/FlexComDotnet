using FlexComDotnet.Core.Features.Driver.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Driver;

public class SerialMonitorPortIdentityTests
{
    [Fact]
    public void ResolveInstanceId_UsesDevicePortNameWithoutRequiringWmiOrFriendlyName()
    {
        var devices = new[]
        {
            new SerialMonitorPortIdentity.PortDevice("COM1", @"ACPI\PNP0501\0"),
            new SerialMonitorPortIdentity.PortDevice("COM10", @"VSBC7\DEVICES\0000"),
            new SerialMonitorPortIdentity.PortDevice("COM20", @"VSBC7\DEVICES\0001")
        };

        SerialMonitorPortIdentity.ResolveInstanceId("com10", devices)
            .Should().Be(@"VSBC7\DEVICES\0000");
        SerialMonitorPortIdentity.ResolveInstanceId("COM2", devices).Should().BeNull();
        SerialMonitorPortIdentity.ResolveInstanceId("", devices).Should().BeNull();
    }
}
