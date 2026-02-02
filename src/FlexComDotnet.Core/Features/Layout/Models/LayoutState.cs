using System.Text.Json.Serialization;

namespace FlexComDotnet.Core.Features.Layout.Models;

/// <summary>
/// 布局状态模型，用于持久化存储
/// </summary>
public class LayoutState
{
    /// <summary>
    /// 所有面板信息列表
    /// </summary>
    [JsonPropertyName("panels")]
    public List<PanelInfo> Panels { get; set; } = [];

    /// <summary>
    /// 左侧区域宽度
    /// </summary>
    [JsonPropertyName("leftZoneWidth")]
    public double LeftZoneWidth { get; set; } = 280;

    /// <summary>
    /// 右侧区域宽度
    /// </summary>
    [JsonPropertyName("rightZoneWidth")]
    public double RightZoneWidth { get; set; } = 300;

    /// <summary>
    /// 底部区域高度
    /// </summary>
    [JsonPropertyName("bottomZoneHeight")]
    public double BottomZoneHeight { get; set; } = 200;

    /// <summary>
    /// 左侧区域是否折叠
    /// </summary>
    [JsonPropertyName("isLeftZoneCollapsed")]
    public bool IsLeftZoneCollapsed { get; set; }

    /// <summary>
    /// 右侧区域是否折叠
    /// </summary>
    [JsonPropertyName("isRightZoneCollapsed")]
    public bool IsRightZoneCollapsed { get; set; }

    /// <summary>
    /// 底部区域是否折叠
    /// </summary>
    [JsonPropertyName("isBottomZoneCollapsed")]
    public bool IsBottomZoneCollapsed { get; set; }
}
