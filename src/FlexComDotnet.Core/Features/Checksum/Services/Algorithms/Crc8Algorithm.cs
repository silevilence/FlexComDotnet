using FlexComDotnet.Core.Features.Checksum.Models;

namespace FlexComDotnet.Core.Features.Checksum.Services.Algorithms;

/// <summary>
/// CRC-8 标准校验算法
/// 多项式: 0x07 (x^8 + x^2 + x + 1)
/// 初始值: 0x00
/// </summary>
public class Crc8Algorithm : ChecksumAlgorithmBase
{
    private const byte Polynomial = 0x07;
    private const byte InitialValue = 0x00;
    
    private static readonly object LockObj = new();
    private static byte[]? _crcTable;

    /// <inheritdoc/>
    public override ChecksumAlgorithmType Type => ChecksumAlgorithmType.Crc8;

    /// <inheritdoc/>
    public override string DisplayName => "CRC-8";

    /// <inheritdoc/>
    public override string Description => "CRC-8 标准校验 (多项式: 0x07, 初始值: 0x00)";

    /// <inheritdoc/>
    public override int ResultLength => 1;

    /// <inheritdoc/>
    public override byte[] Calculate(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return [InitialValue];
        }

        var table = GetCrcTable();
        byte crc = InitialValue;

        foreach (var b in data)
        {
            crc = table[crc ^ b];
        }

        return [crc];
    }

    private static byte[] GetCrcTable()
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

            var table = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                byte crc = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc = (byte)((crc << 1) ^ Polynomial);
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
