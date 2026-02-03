namespace FlexComDotnet.Core.Features.Checksum.Models;

/// <summary>
/// 校验和算法类型枚举
/// </summary>
public enum ChecksumAlgorithmType
{
    /// <summary>
    /// 8位累加和校验
    /// </summary>
    Sum8,

    /// <summary>
    /// 16位累加和校验
    /// </summary>
    Sum16,

    /// <summary>
    /// XOR 异或校验
    /// </summary>
    Xor,

    /// <summary>
    /// CRC-8 标准校验
    /// </summary>
    Crc8,

    /// <summary>
    /// CRC-16 MODBUS 校验
    /// </summary>
    Crc16Modbus,

    /// <summary>
    /// CRC-16 CCITT-FALSE 校验
    /// </summary>
    Crc16CcittFalse,

    /// <summary>
    /// CRC-16 XMODEM 校验
    /// </summary>
    Crc16Xmodem,

    /// <summary>
    /// CRC-32 标准校验
    /// </summary>
    Crc32,

    /// <summary>
    /// MD5 摘要算法
    /// </summary>
    Md5,

    /// <summary>
    /// SHA-1 摘要算法
    /// </summary>
    Sha1,

    /// <summary>
    /// SHA-256 摘要算法
    /// </summary>
    Sha256
}
