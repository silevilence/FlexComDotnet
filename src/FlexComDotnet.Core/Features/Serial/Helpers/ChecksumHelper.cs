using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Core.Features.Serial.Helpers;

/// <summary>
/// 校验和计算帮助类
/// </summary>
public static class ChecksumHelper
{
    /// <summary>
    /// 计算 Sum8 累加和校验
    /// </summary>
    /// <param name="data">数据</param>
    /// <returns>校验和 (低 8 位)</returns>
    public static byte CalculateSum8(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return 0;
        }

        int sum = 0;
        foreach (var b in data)
        {
            sum += b;
        }

        return (byte)(sum & 0xFF);
    }

    /// <summary>
    /// 计算 CRC16 MODBUS 校验
    /// </summary>
    /// <param name="data">数据</param>
    /// <returns>CRC16 值</returns>
    public static ushort CalculateCrc16Modbus(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return 0xFFFF;
        }

        ushort crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= b;

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        return crc;
    }

    /// <summary>
    /// 根据校验类型追加校验和到数据末尾
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="checksumType">校验类型</param>
    /// <returns>追加校验和后的数据</returns>
    public static byte[] AppendChecksum(byte[] data, ChecksumType checksumType)
    {
        if (data == null)
        {
            return [];
        }

        return checksumType switch
        {
            ChecksumType.None => data,
            ChecksumType.Sum8 => AppendSum8(data),
            ChecksumType.Crc16Modbus => AppendCrc16Modbus(data),
            _ => data
        };
    }

    private static byte[] AppendSum8(byte[] data)
    {
        var result = new byte[data.Length + 1];
        Array.Copy(data, result, data.Length);
        result[data.Length] = CalculateSum8(data);
        return result;
    }

    private static byte[] AppendCrc16Modbus(byte[] data)
    {
        var result = new byte[data.Length + 2];
        Array.Copy(data, result, data.Length);
        
        var crc = CalculateCrc16Modbus(data);
        // 小端序: 低字节在前
        result[data.Length] = (byte)(crc & 0xFF);
        result[data.Length + 1] = (byte)(crc >> 8);
        
        return result;
    }
}
