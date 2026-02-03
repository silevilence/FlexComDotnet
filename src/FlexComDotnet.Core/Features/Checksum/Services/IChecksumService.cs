using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services;

/// <summary>
/// 校验和算法服务接口
/// </summary>
public interface IChecksumService
{
    /// <summary>
    /// 获取所有可用的算法
    /// </summary>
    IReadOnlyList<IChecksumAlgorithm> GetAllAlgorithms();

    /// <summary>
    /// 根据类型获取算法
    /// </summary>
    /// <param name="type">算法类型</param>
    /// <returns>算法实例</returns>
    IChecksumAlgorithm GetAlgorithm(ChecksumAlgorithmType type);

    /// <summary>
    /// 使用指定算法计算校验值
    /// </summary>
    /// <param name="type">算法类型</param>
    /// <param name="data">输入数据</param>
    /// <returns>校验值字节数组</returns>
    byte[] Calculate(ChecksumAlgorithmType type, byte[] data);

    /// <summary>
    /// 使用指定算法计算校验值并返回十六进制字符串
    /// </summary>
    /// <param name="type">算法类型</param>
    /// <param name="data">输入数据</param>
    /// <returns>十六进制字符串结果</returns>
    string CalculateAsHexString(ChecksumAlgorithmType type, byte[] data);
}
