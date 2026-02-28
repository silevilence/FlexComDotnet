using FlexComDotnet.Core.Features.Protocol.Models;

namespace FlexComDotnet.Core.Features.Protocol.Services;

/// <summary>
/// 协议解析器接口 (策略模式)
/// </summary>
public interface IProtocolParser
{
    /// <summary>
    /// 解析器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 解析器描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 关联的帧定义
    /// </summary>
    FrameDefinition Definition { get; }

    /// <summary>
    /// 解析帧数据
    /// </summary>
    /// <param name="frame">原始帧字节数组</param>
    /// <returns>解析结果</returns>
    ParsedFrame Parse(byte[] frame);

    /// <summary>
    /// 验证帧是否符合协议格式
    /// </summary>
    /// <param name="frame">原始帧字节数组</param>
    /// <returns>是否有效</returns>
    bool Validate(byte[] frame);

    /// <summary>
    /// 尝试从数据流中提取完整帧
    /// </summary>
    /// <param name="buffer">数据缓冲区</param>
    /// <param name="frame">提取的完整帧</param>
    /// <param name="consumedBytes">消耗的字节数</param>
    /// <returns>是否成功提取</returns>
    bool TryExtractFrame(byte[] buffer, out byte[] frame, out int consumedBytes);
}
