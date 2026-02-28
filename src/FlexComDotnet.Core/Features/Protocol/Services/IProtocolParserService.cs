using FlexComDotnet.Core.Features.Protocol.Models;

namespace FlexComDotnet.Core.Features.Protocol.Services;

/// <summary>
/// 协议解析服务接口
/// </summary>
public interface IProtocolParserService
{
    /// <summary>
    /// 获取所有已注册的解析器
    /// </summary>
    IReadOnlyList<IProtocolParser> GetAllParsers();

    /// <summary>
    /// 根据名称获取解析器
    /// </summary>
    IProtocolParser? GetParser(string name);

    /// <summary>
    /// 注册新的帧定义并创建解析器
    /// </summary>
    IProtocolParser RegisterDefinition(FrameDefinition definition);

    /// <summary>
    /// 批量加载帧定义
    /// </summary>
    void LoadDefinitions(IEnumerable<FrameDefinition> definitions);

    /// <summary>
    /// 移除解析器
    /// </summary>
    bool RemoveParser(string name);

    /// <summary>
    /// 使用指定解析器解析帧
    /// </summary>
    ParsedFrame Parse(string parserName, byte[] frame);

    /// <summary>
    /// 自动检测并解析帧 (尝试所有已注册的解析器)
    /// </summary>
    ParsedFrame? AutoParse(byte[] frame);

    /// <summary>
    /// 获取所有帧定义
    /// </summary>
    IReadOnlyList<FrameDefinition> GetAllDefinitions();

    /// <summary>
    /// 保存帧定义到文件
    /// </summary>
    Task SaveDefinitionAsync(FrameDefinition definition, string filePath);

    /// <summary>
    /// 从文件加载帧定义
    /// </summary>
    Task<FrameDefinition?> LoadDefinitionAsync(string filePath);

    /// <summary>
    /// 解析器注册事件
    /// </summary>
    event EventHandler<ParserRegisteredEventArgs>? ParserRegistered;

    /// <summary>
    /// 解析器移除事件
    /// </summary>
    event EventHandler<ParserRemovedEventArgs>? ParserRemoved;
}

/// <summary>
/// 解析器注册事件参数
/// </summary>
public class ParserRegisteredEventArgs : EventArgs
{
    public IProtocolParser Parser { get; }

    public ParserRegisteredEventArgs(IProtocolParser parser)
    {
        Parser = parser;
    }
}

/// <summary>
/// 解析器移除事件参数
/// </summary>
public class ParserRemovedEventArgs : EventArgs
{
    public string ParserName { get; }

    public ParserRemovedEventArgs(string parserName)
    {
        ParserName = parserName;
    }
}
