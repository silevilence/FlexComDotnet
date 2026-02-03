using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services;

/// <summary>
/// 校验和算法接口 (策略模式)
/// </summary>
public interface IChecksumAlgorithm
{
    /// <summary>
    /// 算法类型
    /// </summary>
    ChecksumAlgorithmType Type { get; }

    /// <summary>
    /// 算法显示名称
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 算法描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 结果字节长度
    /// </summary>
    int ResultLength { get; }

    /// <summary>
    /// 计算校验值
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <returns>校验值字节数组</returns>
    byte[] Calculate(byte[] data);

    /// <summary>
    /// 计算校验值并返回十六进制字符串
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <returns>十六进制字符串结果</returns>
    string CalculateAsHexString(byte[] data);
}
