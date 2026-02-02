using FlexComDotnet.Core.Features.Layout.Models;

namespace FlexComDotnet.Core.Features.Layout.Services;

/// <summary>
/// 面板管理器接口
/// </summary>
public interface IPanelManager
{
    /// <summary>
    /// 获取所有面板信息
    /// </summary>
    IReadOnlyList<PanelInfo> Panels { get; }

    /// <summary>
    /// 布局状态变更事件
    /// </summary>
    event EventHandler? LayoutChanged;

    /// <summary>
    /// 注册面板
    /// </summary>
    /// <param name="panel">面板信息</param>
    void RegisterPanel(PanelInfo panel);

    /// <summary>
    /// 移除面板
    /// </summary>
    /// <param name="panelId">面板ID</param>
    void RemovePanel(string panelId);

    /// <summary>
    /// 获取指定区域的面板列表
    /// </summary>
    /// <param name="zone">区域</param>
    /// <returns>该区域的面板列表（按 Order 排序）</returns>
    IReadOnlyList<PanelInfo> GetPanelsInZone(PanelZone zone);

    /// <summary>
    /// 移动面板到指定区域
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <param name="targetZone">目标区域</param>
    /// <param name="targetOrder">目标排序位置（可选，默认追加到末尾）</param>
    /// <returns>是否移动成功</returns>
    bool MovePanel(string panelId, PanelZone targetZone, int? targetOrder = null);

    /// <summary>
    /// 设置面板展开/折叠状态
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <param name="isExpanded">是否展开</param>
    void SetPanelExpanded(string panelId, bool isExpanded);

    /// <summary>
    /// 切换面板展开/折叠状态
    /// </summary>
    /// <param name="panelId">面板ID</param>
    void TogglePanelExpanded(string panelId);

    /// <summary>
    /// 设置面板可见性
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <param name="isVisible">是否可见</param>
    void SetPanelVisibility(string panelId, bool isVisible);

    /// <summary>
    /// 获取指定面板信息
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <returns>面板信息，不存在则返回 null</returns>
    PanelInfo? GetPanel(string panelId);

    /// <summary>
    /// 获取当前布局状态
    /// </summary>
    LayoutState GetLayoutState();

    /// <summary>
    /// 恢复布局状态
    /// </summary>
    /// <param name="state">布局状态</param>
    void RestoreLayoutState(LayoutState state);

    /// <summary>
    /// 设置区域尺寸
    /// </summary>
    void SetZoneSize(PanelZone zone, double size);

    /// <summary>
    /// 获取区域尺寸
    /// </summary>
    double GetZoneSize(PanelZone zone);

    /// <summary>
    /// 设置区域折叠状态
    /// </summary>
    void SetZoneCollapsed(PanelZone zone, bool isCollapsed);

    /// <summary>
    /// 获取区域折叠状态
    /// </summary>
    bool IsZoneCollapsed(PanelZone zone);

    /// <summary>
    /// 切换区域折叠状态
    /// </summary>
    void ToggleZoneCollapsed(PanelZone zone);

    /// <summary>
    /// 设置面板为浮动状态（脱离 Dock）
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <param name="x">浮动窗口 X 坐标</param>
    /// <param name="y">浮动窗口 Y 坐标</param>
    /// <param name="width">浮动窗口宽度</param>
    /// <param name="height">浮动窗口高度</param>
    void SetPanelFloating(string panelId, double x, double y, double width, double height);

    /// <summary>
    /// 将面板停靠回指定区域
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <param name="zone">目标区域</param>
    void DockPanel(string panelId, PanelZone zone);

    /// <summary>
    /// 更新浮动面板的位置和尺寸
    /// </summary>
    /// <param name="panelId">面板ID</param>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    void UpdateFloatingPanelBounds(string panelId, double x, double y, double width, double height);

    /// <summary>
    /// 获取所有浮动面板
    /// </summary>
    IReadOnlyList<PanelInfo> GetFloatingPanels();

    /// <summary>
    /// 切换面板可见性
    /// </summary>
    /// <param name="panelId">面板ID</param>
    void TogglePanelVisibility(string panelId);
}
