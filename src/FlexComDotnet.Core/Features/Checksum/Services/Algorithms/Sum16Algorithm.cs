using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// Sum16 累加和校验算法
/// </summary>
public class Sum16Algorithm : ChecksumAlgorithmBase
{
    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Sum16;

    /// <inheritdoc/>
    public override string DisplayName => "Sum16 (16位累加和)";

    /// <inheritdoc/>
    public override string Description => "16位累加和校验，将所有字节相加取低16位，大端序输出";

    /// <inheritdoc/>
    public override int ResultLength => 2;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return [0, 0];
        }

        int sum = 0;
        foreach (var b in data)
        {
            sum += b;
        }

        ushort result = (ushort)(sum & 0xFFFF);
        // 大端序: 高字节在前
        return [(byte)(result >> 8), (byte)(result & 0xFF)];
    }
}
