namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 数据类型枚举
/// </summary>
public enum DataType
{
    /// <summary>
    /// 无符号8位整数
    /// </summary>
    UInt8,

    /// <summary>
    /// 有符号8位整数
    /// </summary>
    Int8,

    /// <summary>
    /// 无符号16位整数
    /// </summary>
    UInt16,

    /// <summary>
    /// 有符号16位整数
    /// </summary>
    Int16,

    /// <summary>
    /// 无符号32位整数
    /// </summary>
    UInt32,

    /// <summary>
    /// 有符号32位整数
    /// </summary>
    Int32,

    /// <summary>
    /// 无符号64位整数
    /// </summary>
    UInt64,

    /// <summary>
    /// 有符号64位整数
    /// </summary>
    Int64,

    /// <summary>
    /// 32位浮点数
    /// </summary>
    Float,

    /// <summary>
    /// 64位浮点数
    /// </summary>
    Double,

    /// <summary>
    /// 原始字节数组
    /// </summary>
    Bytes,

    /// <summary>
    /// ASCII 字符串
    /// </summary>
    AsciiString,

    /// <summary>
    /// 布尔值 (单字节)
    /// </summary>
    Bool
}
