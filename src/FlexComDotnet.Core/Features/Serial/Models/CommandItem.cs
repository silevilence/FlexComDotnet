namespace FlexComDotnet.Core.Features.Serial.Models;

/// <summary>
/// 预设指令项模型
/// </summary>
public class CommandItem
{
    /// <summary>
    /// 唯一标识符（LiteDB 主键）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 指令名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 指令内容（Hex 或 ASCII 字符串）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 指令描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用 Hex 模式发送
    /// </summary>
    public bool IsHexMode { get; set; }

    /// <summary>
    /// 是否启用（用于快速启用/禁用）
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
