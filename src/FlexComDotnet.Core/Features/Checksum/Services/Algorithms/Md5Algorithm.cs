using System.Security.Cryptography;
using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// MD5 摘要算法
/// </summary>
public class Md5Algorithm : ChecksumAlgorithmBase
{
    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Md5;

    /// <inheritdoc/>
    public override string DisplayName => "MD5";

    /// <inheritdoc/>
    public override string Description => "MD5 消息摘要算法，生成 128 位 (16 字节) 哈希值";

    /// <inheritdoc/>
    public override int ResultLength => 16;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            data = [];
        }

        return MD5.HashData(data);
    }
}
