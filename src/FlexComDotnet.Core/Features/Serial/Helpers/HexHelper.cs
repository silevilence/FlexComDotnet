using System.Text;
using System.Text.RegularExpressions;

namespace FlexComDotnet.Core.Features.Serial.Helpers;

/// <summary>
/// 十六进制与 ASCII 数据转换工具类
/// </summary>
public static partial class HexHelper
{
    /// <summary>
    /// 将字节数组转换为十六进制字符串
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <param name="separator">分隔符，默认为空格</param>
    /// <returns>十六进制字符串</returns>
    public static string BytesToHexString(byte[] bytes, string separator = " ")
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(separator, bytes.Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// 将十六进制字符串转换为字节数组
    /// </summary>
    /// <param name="hexString">十六进制字符串，支持带空格或不带空格</param>
    /// <returns>字节数组，如果输入无效则返回空数组</returns>
    public static byte[] HexStringToBytes(string hexString)
    {
        if (string.IsNullOrEmpty(hexString))
        {
            return [];
        }

        // 移除所有空格
        var cleanHex = hexString.Replace(" ", "").Trim();

        if (string.IsNullOrEmpty(cleanHex))
        {
            return [];
        }

        // 验证是否为有效的十六进制字符
        if (!HexRegex().IsMatch(cleanHex))
        {
            return [];
        }

        // 如果是奇数长度，在前面补0
        if (cleanHex.Length % 2 != 0)
        {
            cleanHex = "0" + cleanHex;
        }

        try
        {
            var bytes = new byte[cleanHex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(cleanHex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 将字节数组转换为 ASCII 字符串
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <param name="replacementChar">用于替换不可打印字符的字符，默认不替换</param>
    /// <returns>ASCII 字符串</returns>
    public static string BytesToAsciiString(byte[] bytes, char? replacementChar = null)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        if (replacementChar.HasValue)
        {
            var sb = new StringBuilder(bytes.Length);
            foreach (var b in bytes)
            {
                // 可打印 ASCII 字符范围: 32-126
                if (b >= 32 && b <= 126)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append(replacementChar.Value);
                }
            }
            return sb.ToString();
        }

        return Encoding.ASCII.GetString(bytes);
    }

    /// <summary>
    /// 将 ASCII 字符串转换为字节数组
    /// </summary>
    /// <param name="text">ASCII 文本</param>
    /// <returns>字节数组</returns>
    public static byte[] AsciiStringToBytes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return Encoding.ASCII.GetBytes(text);
    }

    /// <summary>
    /// 验证字符串是否为有效的十六进制格式
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidHexString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        var cleanHex = input.Replace(" ", "");
        if (string.IsNullOrEmpty(cleanHex))
        {
            return true;
        }

        return HexRegex().IsMatch(cleanHex);
    }

    [GeneratedRegex("^[0-9A-Fa-f]+$")]
    private static partial Regex HexRegex();
}
