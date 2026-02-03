using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// XOR 异或校验算法
/// </summary>
public class XorAlgorithm : ChecksumAlgorithmBase
{
    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Xor;

    /// <inheritdoc/>
    public override string DisplayName => "XOR (异或校验)";

    /// <inheritdoc/>
    public override string Description => "将所有字节进行异或运算，常用于简单校验";

    /// <inheritdoc/>
    public override int ResultLength => 1;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return [0];
        }

        byte xor = 0;
        foreach (var b in data)
        {
            xor ^= b;
        }

        return [xor];
    }
}
