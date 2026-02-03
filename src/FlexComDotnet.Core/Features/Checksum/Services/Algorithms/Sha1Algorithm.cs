using System.Security.Cryptography;
using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// SHA-1 摘要算法
/// </summary>
public class Sha1Algorithm : ChecksumAlgorithmBase
{
    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Sha1;

    /// <inheritdoc/>
    public override string DisplayName => "SHA-1";

    /// <inheritdoc/>
    public override string Description => "SHA-1 安全哈希算法，生成 160 位 (20 字节) 哈希值";

    /// <inheritdoc/>
    public override int ResultLength => 20;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            data = [];
        }

        return SHA1.HashData(data);
    }
}
