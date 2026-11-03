using System.Buffers.Binary;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.ProcessMonitor;

public class ProcessMonitorDiagnosticsTests
{
    [Fact]
    public void FromWire_ShouldSeparateLostObservationsFromTrackingWarnings()
    {
        var bytes = new byte[64];
        for (var i = 0; i < 8; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), (uint)(i + 1));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 256);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(40), 100);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(48), 2000);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(56), 90);
        var result = ProcessMonitorDiagnostics.FromWire(bytes);
        result.LostObservations.Should().Be(28);
        result.RetirementCapacity.Should().Be(8);
        result.QueueHighWater.Should().Be(256);
        result.PublishCalls.Should().Be(100);
        result.PublishTicks.Should().Be(2000);
        result.MaxPublishTicks.Should().Be(90);
    }
}
