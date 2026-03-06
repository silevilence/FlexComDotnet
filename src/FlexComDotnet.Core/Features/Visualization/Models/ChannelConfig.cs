using System.Text.Json.Serialization;

namespace FlexComDotnet.Core.Features.Visualization.Models;

/// <summary>
/// 可视化通道配置
/// </summary>
public class ChannelConfig
{
    /// <summary>
    /// 通道唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 绑定的协议字段名称
    /// </summary>
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 通道显示名称
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 通道颜色 (十六进制字符串, 如 "#FF0000")
    /// </summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#2196F3";

    /// <summary>
    /// 通道是否可见
    /// </summary>
    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 通道排序顺序
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// 线条宽度
    /// </summary>
    [JsonPropertyName("lineWidth")]
    public float LineWidth { get; set; } = 2f;
}
