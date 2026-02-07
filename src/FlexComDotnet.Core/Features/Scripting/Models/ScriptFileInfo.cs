using System.Text.Json.Serialization;

namespace FlexComDotnet.Core.Features.Scripting.Models;

/// <summary>
/// 脚本文件信息
/// </summary>
public class ScriptFileInfo
{
    /// <summary>
    /// 脚本唯一标识
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 脚本显示名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 脚本文件路径（相对于脚本目录）
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 脚本描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    [JsonPropertyName("lastModifiedAt")]
    public DateTime LastModifiedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建副本
    /// </summary>
    public ScriptFileInfo Clone()
    {
        return new ScriptFileInfo
        {
            Id = Id,
            Name = Name,
            FilePath = FilePath,
            Description = Description,
            CreatedAt = CreatedAt,
            LastModifiedAt = LastModifiedAt
        };
    }
}
