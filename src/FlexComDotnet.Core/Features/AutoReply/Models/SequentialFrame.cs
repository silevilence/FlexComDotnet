namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 顺序回复帧模型
/// </summary>
public class SequentialFrame
{
    /// <summary>
    /// 帧唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 帧名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 帧内容（Hex 或 ASCII 字符串）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 内容是否为 Hex 格式
    /// </summary>
    public bool IsHexMode { get; set; } = true;

    /// <summary>
    /// 是否启用此帧
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 帧描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
