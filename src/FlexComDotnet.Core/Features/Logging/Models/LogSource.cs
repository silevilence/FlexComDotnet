namespace FlexComDotnet.Core.Features.Logging.Models;

/// <summary>
/// 日志来源模块
/// </summary>
public enum LogSource
{
    /// <summary>
    /// 系统 (应用启动/关闭等)
    /// </summary>
    System,

    /// <summary>
    /// 串口
    /// </summary>
    Serial,

    /// <summary>
    /// 网络
    /// </summary>
    Network,

    /// <summary>
    /// 脚本
    /// </summary>
    Script,

    /// <summary>
    /// 自动回复
    /// </summary>
    AutoReply,

    /// <summary>
    /// 协议解析
    /// </summary>
    Protocol,

    /// <summary>
    /// 数据可视化
    /// </summary>
    Visualization
}
