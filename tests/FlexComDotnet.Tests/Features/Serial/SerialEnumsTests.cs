using FlexComDotnet.Core.Features.Serial.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

public class SerialEnumsTests
{
    [Theory]
    [InlineData(BaudRate.Baud1200, 1200)]
    [InlineData(BaudRate.Baud9600, 9600)]
    [InlineData(BaudRate.Baud115200, 115200)]
    [InlineData(BaudRate.Baud921600, 921600)]
    public void BaudRate_ShouldHaveCorrectIntValue(BaudRate baudRate, int expected)
    {
        ((int)baudRate).Should().Be(expected);
    }

    [Theory]
    [InlineData(DataBitsOption.Five, 5)]
    [InlineData(DataBitsOption.Six, 6)]
    [InlineData(DataBitsOption.Seven, 7)]
    [InlineData(DataBitsOption.Eight, 8)]
    public void DataBits_ShouldHaveCorrectIntValue(DataBitsOption dataBits, int expected)
    {
        ((int)dataBits).Should().Be(expected);
    }

    [Theory]
    [InlineData(StopBitsOption.One, 1)]
    [InlineData(StopBitsOption.Two, 2)]
    [InlineData(StopBitsOption.OnePointFive, 3)]
    public void StopBits_ShouldHaveCorrectIntValue(StopBitsOption stopBits, int expected)
    {
        ((int)stopBits).Should().Be(expected);
    }

    [Theory]
    [InlineData(ParityOption.None, 0)]
    [InlineData(ParityOption.Odd, 1)]
    [InlineData(ParityOption.Even, 2)]
    [InlineData(ParityOption.Mark, 3)]
    [InlineData(ParityOption.Space, 4)]
    public void Parity_ShouldHaveCorrectIntValue(ParityOption parity, int expected)
    {
        ((int)parity).Should().Be(expected);
    }

    [Theory]
    [InlineData(FlowControlOption.None, 0)]
    [InlineData(FlowControlOption.XonXoff, 1)]
    [InlineData(FlowControlOption.RtsCts, 2)]
    [InlineData(FlowControlOption.DtrDsr, 3)]
    public void FlowControl_ShouldHaveCorrectIntValue(FlowControlOption flowControl, int expected)
    {
        ((int)flowControl).Should().Be(expected);
    }
}
