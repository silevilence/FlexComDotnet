using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

public class FrameDelimiterTests
{
    [Fact]
    public void AppendByte_FirstByte_ShouldStartNewFrame()
    {
        var delimiter = new FrameDelimiter(10, 1024);
        byte[][]? receivedFrames = null;
        delimiter.FrameCompleted += f => receivedFrames = [..receivedFrames ?? [], f];

        var now = DateTime.UtcNow;
        delimiter.AppendByte(0xAA, now);

        // 单字节不会立即产出帧（等待间隔或 Flush）
        receivedFrames.Should().BeNull();
    }

    [Fact]
    public void AppendBytes_WithinInterval_ShouldAccumulate()
    {
        var delimiter = new FrameDelimiter(100, 1024);
        var frames = new List<byte[]>();
        delimiter.FrameCompleted += f => frames.Add(f);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        delimiter.AppendByte(0x01, baseTime);
        delimiter.AppendByte(0x02, baseTime.AddMilliseconds(50));  // 间隔 50ms < 100ms
        delimiter.AppendByte(0x03, baseTime.AddMilliseconds(90));  // 间隔 40ms < 100ms

        // 强制触发 Flush
        delimiter.Flush();

        frames.Should().HaveCount(1);
        frames[0].Should().Equal(0x01, 0x02, 0x03);
    }

    [Fact]
    public void AppendBytes_ExceedingInterval_ShouldSplit()
    {
        var delimiter = new FrameDelimiter(100, 1024);
        var frames = new List<byte[]>();
        delimiter.FrameCompleted += f => frames.Add(f);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        delimiter.AppendByte(0x01, baseTime);
        delimiter.AppendByte(0x02, baseTime.AddMilliseconds(50));
        // 间隔 150ms > 100ms → 截断
        delimiter.AppendByte(0x03, baseTime.AddMilliseconds(200));
        delimiter.Flush();

        frames.Should().HaveCount(2);
        frames[0].Should().Equal(0x01, 0x02);
        frames[1].Should().Equal(0x03);
    }

    [Fact]
    public void AppendBytes_ExceedingMaxLength_ShouldForceSplit()
    {
        var delimiter = new FrameDelimiter(1000, 3);
        var frames = new List<byte[]>();
        delimiter.FrameCompleted += f => frames.Add(f);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        delimiter.AppendByte(0x01, baseTime);
        delimiter.AppendByte(0x02, baseTime.AddMilliseconds(1));
        delimiter.AppendByte(0x03, baseTime.AddMilliseconds(2));   // 达到 MaxFrameBytes=3 → 强制截断
        delimiter.AppendByte(0x04, baseTime.AddMilliseconds(3));   // 开始新帧
        delimiter.Flush();

        frames.Should().HaveCount(2);
        frames[0].Should().Equal(0x01, 0x02, 0x03);
        frames[1].Should().Equal(0x04);
    }

    [Fact]
    public void Flush_WithEmptyBuffer_ShouldNotTrigger()
    {
        var delimiter = new FrameDelimiter(100, 1024);
        var frameTriggered = false;
        delimiter.FrameCompleted += _ => frameTriggered = true;

        delimiter.Flush();

        frameTriggered.Should().BeFalse();
    }

    [Fact]
    public void Reset_ShouldClearBuffer()
    {
        var delimiter = new FrameDelimiter(100, 1024);
        var frames = new List<byte[]>();
        delimiter.FrameCompleted += f => frames.Add(f);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        delimiter.AppendByte(0x01, baseTime);
        delimiter.Reset();
        delimiter.Flush();

        frames.Should().BeEmpty();
    }
}
