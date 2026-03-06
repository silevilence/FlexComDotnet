using System.Text.Json.Serialization;

namespace FlexComDotnet.Core.Features.Visualization.Models;

/// <summary>
/// 数据可视化配置
/// </summary>
public class VisualizationConfig
{
    /// <summary>
    /// 通道配置列表
    /// </summary>
    [JsonPropertyName("channels")]
    public List<ChannelConfig> Channels { get; set; } = [];

    /// <summary>
    /// 每通道最大数据点数
    /// </summary>
    [JsonPropertyName("maxDataPoints")]
    public int MaxDataPoints { get; set; } = 1000;

    /// <summary>
    /// 选中的协议解析器名称
    /// </summary>
    [JsonPropertyName("selectedParserName")]
    public string? SelectedParserName { get; set; }
}
