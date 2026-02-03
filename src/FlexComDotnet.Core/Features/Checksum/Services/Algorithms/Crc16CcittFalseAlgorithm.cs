using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// CRC-16/CCITT-FALSE 校验算法
/// 多项式: 0x1021
/// 初始值: 0xFFFF
/// 输入反转: 否
/// 输出反转: 否
/// 结果异或: 0x0000
/// 输出: 大端序 (高字节在前)
/// </summary>
public class Crc16CcittFalseAlgorithm : ChecksumAlgorithmBase
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;
    
    private static readonly object LockObj = new();
    private static ushort[]? _crcTable;

    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Crc16CcittFalse;

    /// <inheritdoc/>
    public override string DisplayName => "CRC-16/CCITT-FALSE";

    /// <inheritdoc/>
    public override string Description => "CRC-16 CCITT-FALSE 校验 (多项式: 0x1021, 初始值: 0xFFFF, 大端序)";

    /// <inheritdoc/>
    public override int ResultLength => 2;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return [(byte)(InitialValue >> 8), (byte)(InitialValue & 0xFF)];
        }

        var table = GetCrcTable();
        ushort crc = InitialValue;

        foreach (var b in data)
        {
            crc = (ushort)((crc << 8) ^ table[(crc >> 8) ^ b]);
        }

        // 大端序: 高字节在前
        return [(byte)(crc >> 8), (byte)(crc & 0xFF)];
    }

    private static ushort[] GetCrcTable()
    {
        if (_crcTable != null)
        {
            return _crcTable;
        }

        lock (LockObj)
        {
            if (_crcTable != null)
            {
                return _crcTable;
            }

            var table = new ushort[256];
            for (int i = 0; i < 256; i++)
            {
                ushort crc = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ Polynomial);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
                table[i] = crc;
            }
            _crcTable = table;
            return _crcTable;
        }
    }
}
