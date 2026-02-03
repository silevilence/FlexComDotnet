using System.Security.Cryptography;
using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// SHA-256 摘要算法
/// </summary>
public class Sha256Algorithm : ChecksumAlgorithmBase
{
    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Sha256;

    /// <inheritdoc/>
    public override string DisplayName => "SHA-256";

    /// <inheritdoc/>
    public override string Description => "SHA-256 安全哈希算法，生成 256 位 (32 字节) 哈希值";

    /// <inheritdoc/>
    public override int ResultLength => 32;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            data = [];
        }

        return SHA256.HashData(data);
    }
}
