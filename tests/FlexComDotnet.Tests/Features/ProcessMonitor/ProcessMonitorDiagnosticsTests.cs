using System.Buffers.Binary;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.ProcessMonitor;

public class ProcessMonitorDiagnosticsTests
{
    [Fact]
    public void FromWire_ShouldSeparateLostObservationsFromTrackingWarnings()
    {
        var bytes = new byte[104];
        for (var i = 0; i < 8; i++) BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(i * 8), (17ul << 32) | (uint)(i + 1));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 256);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(80), 100);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(88), 2000);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(96), 90);
        var result = ProcessMonitorDiagnostics.FromWire(bytes);
        result.LostObservations.Should().Be(28);
        result.RetirementCapacity.Should().Be(8);
        result.QueueHighWater.Should().Be(256);
        result.PublishCalls.Should().Be(100);
        result.PublishTicks.Should().Be(2000);
        result.MaxPublishTicks.Should().Be(90);
    }
}
