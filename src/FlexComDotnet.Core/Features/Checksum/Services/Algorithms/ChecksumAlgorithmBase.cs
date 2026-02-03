using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// 校验和算法基类
/// </summary>
public abstract class ChecksumAlgorithmBase : IChecksumAlgorithm
{
    /// <inheritdoc/>
    public abstract ChecksumAlgorithmType Type { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract int ResultLength { get; }

    /// <inheritdoc/>
    public abstract byte[] Calculate(byte[] data);

    /// <inheritdoc/>
    public virtual string CalculateAsHexString(byte[] data)
    {
        var result = Calculate(data);
        return string.Join(" ", result.Select(b => b.ToString("X2")));
    }
}
