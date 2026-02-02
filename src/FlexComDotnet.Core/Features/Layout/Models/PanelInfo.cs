using System.Text.Json.Serialization;

namespace FlexComDotnet.Core.Features.Layout.Models;

/// <summary>
/// 面板信息模型
/// </summary>
public class PanelInfo
{
    /// <summary>
    /// 面板唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 面板显示标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 面板图标 (Unicode 字符或图标名称)
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 面板所在区域
    /// </summary>
    [JsonPropertyName("zone")]
    public PanelZone Zone { get; set; } = PanelZone.Left;

    /// <summary>
    /// 面板在区域中的排序顺序
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// 面板是否展开
    /// </summary>
    [JsonPropertyName("isExpanded")]
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// 面板是否可移动 (某些面板如串口配置应固定)
    /// </summary>
    [JsonPropertyName("isMovable")]
    public bool IsMovable { get; set; } = true;

    /// <summary>
    /// 面板是否可见
    /// </summary>
    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 面板是否处于浮动窗口状态（脱离 Dock）
    /// </summary>
    [JsonPropertyName("isFloating")]
    public bool IsFloating { get; set; }

    /// <summary>
    /// 浮动窗口的 X 坐标
    /// </summary>
    [JsonPropertyName("floatingX")]
    public double FloatingX { get; set; }

    /// <summary>
    /// 浮动窗口的 Y 坐标
    /// </summary>
    [JsonPropertyName("floatingY")]
    public double FloatingY { get; set; }

    /// <summary>
    /// 浮动窗口的宽度
    /// </summary>
    [JsonPropertyName("floatingWidth")]
    public double FloatingWidth { get; set; } = 300;

    /// <summary>
    /// 浮动窗口的高度
    /// </summary>
    [JsonPropertyName("floatingHeight")]
    public double FloatingHeight { get; set; } = 400;

    /// <summary>
    /// 创建面板信息的副本
    /// </summary>
    public PanelInfo Clone()
    {
        return new PanelInfo
        {
            Id = Id,
            Title = Title,
            Icon = Icon,
            Zone = Zone,
            Order = Order,
            IsExpanded = IsExpanded,
            IsMovable = IsMovable,
            IsVisible = IsVisible,
            IsFloating = IsFloating,
            FloatingX = FloatingX,
            FloatingY = FloatingY,
            FloatingWidth = FloatingWidth,
            FloatingHeight = FloatingHeight
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is PanelInfo other)
        {
            return Id == other.Id;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
