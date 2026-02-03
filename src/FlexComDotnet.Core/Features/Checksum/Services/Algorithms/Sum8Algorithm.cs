using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// Sum8 累加和校验算法
/// </summary>
public class Sum8Algorithm : ChecksumAlgorithmBase
{
    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Sum8;

    /// <inheritdoc/>
    public override string DisplayName => "Sum8 (累加和)";

    /// <inheritdoc/>
    public override string Description => "8位累加和校验，将所有字节相加取低8位";

    /// <inheritdoc/>
    public override int ResultLength => 1;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return [0];
        }

        int sum = 0;
        foreach (var b in data)
        {
            sum += b;
        }

        return [(byte)(sum & 0xFF)];
    }
}
