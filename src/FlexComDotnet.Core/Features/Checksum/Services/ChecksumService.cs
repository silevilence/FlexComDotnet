using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

namespace FlexComDotnet.Core.Features.Checksum.Services;

/// <summary>
/// 校验和算法服务实现
/// </summary>
public class ChecksumService : IChecksumService
{
    private readonly Dictionary<ChecksumAlgorithmType, IChecksumAlgorithm> _algorithms;
    private readonly List<IChecksumAlgorithm> _algorithmList;

    public ChecksumService()
    {
        // 注册所有算法
        _algorithmList =
        [
            new Sum8Algorithm(),
            new Sum16Algorithm(),
            new XorAlgorithm(),
            new Crc8Algorithm(),
            new Crc16ModbusAlgorithm(),
            new Crc16CcittFalseAlgorithm(),
            new Crc16XmodemAlgorithm(),
            new Crc32Algorithm(),
            new Md5Algorithm(),
            new Sha1Algorithm(),
            new Sha256Algorithm()
        ];

        _algorithms = _algorithmList.ToDictionary(a => a.Type);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IChecksumAlgorithm> GetAllAlgorithms() => _algorithmList;

    /// <inheritdoc/>
    public IChecksumAlgorithm GetAlgorithm(ChecksumAlgorithmType type)
    {
        if (_algorithms.TryGetValue(type, out var algorithm))
        {
            return algorithm;
        }

        throw new ArgumentException($"不支持的算法类型: {type}", nameof(type));
    }

    /// <inheritdoc/>
    public byte[] Calculate(ChecksumAlgorithmType type, byte[] data)
    {
        return GetAlgorithm(type).Calculate(data);
    }

    /// <inheritdoc/>
    public string CalculateAsHexString(ChecksumAlgorithmType type, byte[] data)
    {
        return GetAlgorithm(type).CalculateAsHexString(data);
    }
}
