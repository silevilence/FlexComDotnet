namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 协议类型枚举
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// 通用协议 (可自定义帧头、帧尾、校验等)
    /// </summary>
    Generic,

    /// <summary>
    /// DL/T 645-2007 电表协议
    /// </summary>
    Dlt645
}
