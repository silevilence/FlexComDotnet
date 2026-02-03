using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// CRC-32 标准校验算法
/// 多项式: 0xEDB88320 (反转的 0x04C11DB7)
/// 初始值: 0xFFFFFFFF
/// 结果异或: 0xFFFFFFFF
/// 输出: 小端序 (低字节在前)
/// </summary>
public class Crc32Algorithm : ChecksumAlgorithmBase
{
    private const uint Polynomial = 0xEDB88320;
    private const uint InitialValue = 0xFFFFFFFF;
    private const uint FinalXor = 0xFFFFFFFF;
    
    private static readonly object LockObj = new();
    private static uint[]? _crcTable;

    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Crc32;

    /// <inheritdoc/>
    public override string DisplayName => "CRC-32";

    /// <inheritdoc/>
    public override string Description => "CRC-32 标准校验 (多项式: 0x04C11DB7, IEEE 802.3)";

    /// <inheritdoc/>
    public override int ResultLength => 4;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            uint emptyResult = InitialValue ^ FinalXor;
            return [
                (byte)(emptyResult & 0xFF),
                (byte)((emptyResult >> 8) & 0xFF),
                (byte)((emptyResult >> 16) & 0xFF),
                (byte)((emptyResult >> 24) & 0xFF)
            ];
        }

        var table = GetCrcTable();
        uint crc = InitialValue;

        foreach (var b in data)
        {
            crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
        }

        crc ^= FinalXor;

        // 小端序: 低字节在前
        return [
            (byte)(crc & 0xFF),
            (byte)((crc >> 8) & 0xFF),
            (byte)((crc >> 16) & 0xFF),
            (byte)((crc >> 24) & 0xFF)
        ];
    }

    private static uint[] GetCrcTable()
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

            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ Polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
                table[i] = crc;
            }
            _crcTable = table;
            return _crcTable;
        }
    }
}
