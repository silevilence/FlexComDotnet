using FlexComDotnet.Core.Features.Layout.Models;

namespace FlexComDotnet.Core.Features.Layout.Services;

/// <summary>
/// 面板管理器实现
/// </summary>
public class PanelManager : IPanelManager
{
    private readonly List<PanelInfo> _panels = [];
    private double _leftZoneWidth = 280;
    private double _rightZoneWidth = 300;
    private double _bottomZoneHeight = 200;
    private bool _isLeftZoneCollapsed;
    private bool _isRightZoneCollapsed;
    private bool _isBottomZoneCollapsed;

    /// <inheritdoc />
    public IReadOnlyList<PanelInfo> Panels => _panels.AsReadOnly();

    /// <inheritdoc />
    public event EventHandler? LayoutChanged;

    /// <inheritdoc />
    public void RegisterPanel(PanelInfo panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var existingIndex = _panels.FindIndex(p => p.Id == panel.Id);
        if (existingIndex >= 0)
        {
            // 更新现有面板，但保留关键状态
            var existing = _panels[existingIndex];
            var newPanel = panel.Clone();
            
            // 保留 IsMovable 属性
            if (!existing.IsMovable)
            {
                newPanel.IsMovable = false;
            }
            
            // 保留持久化状态（IsVisible, IsFloating, 浮动位置等）
            newPanel.IsVisible = existing.IsVisible;
            newPanel.IsFloating = existing.IsFloating;
            newPanel.FloatingX = existing.FloatingX;
            newPanel.FloatingY = existing.FloatingY;
            newPanel.FloatingWidth = existing.FloatingWidth;
            newPanel.FloatingHeight = existing.FloatingHeight;
            newPanel.Zone = existing.Zone;
            newPanel.Order = existing.Order;
            newPanel.IsExpanded = existing.IsExpanded;
            
            _panels[existingIndex] = newPanel;
        }
        else
        {
            _panels.Add(panel.Clone());
        }

        OnLayoutChanged();
    }

    /// <inheritdoc />
    public void RemovePanel(string panelId)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            _panels.Remove(panel);
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PanelInfo> GetPanelsInZone(PanelZone zone)
    {
        return _panels
            .Where(p => p.Zone == zone)
            .OrderBy(p => p.Order)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public bool MovePanel(string panelId, PanelZone targetZone, int? targetOrder = null)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel == null)
        {
            return false;
        }

        if (!panel.IsMovable)
        {
            return false;
        }

        panel.Zone = targetZone;

        if (targetOrder.HasValue)
        {
            panel.Order = targetOrder.Value;
        }
        else
        {
            // 追加到目标区域末尾
            var maxOrder = _panels
                .Where(p => p.Zone == targetZone && p.Id != panelId)
                .Select(p => p.Order)
                .DefaultIfEmpty(-1)
                .Max();
            panel.Order = maxOrder + 1;
        }

        OnLayoutChanged();
        return true;
    }

    /// <inheritdoc />
    public void SetPanelExpanded(string panelId, bool isExpanded)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            panel.IsExpanded = isExpanded;
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public void TogglePanelExpanded(string panelId)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            panel.IsExpanded = !panel.IsExpanded;
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public void SetPanelVisibility(string panelId, bool isVisible)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            panel.IsVisible = isVisible;
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public PanelInfo? GetPanel(string panelId)
    {
        return _panels.FirstOrDefault(p => p.Id == panelId);
    }

    /// <inheritdoc />
    public LayoutState GetLayoutState()
    {
        return new LayoutState
        {
            Panels = _panels.Select(p => p.Clone()).ToList(),
            LeftZoneWidth = _leftZoneWidth,
            RightZoneWidth = _rightZoneWidth,
            BottomZoneHeight = _bottomZoneHeight,
            IsLeftZoneCollapsed = _isLeftZoneCollapsed,
            IsRightZoneCollapsed = _isRightZoneCollapsed,
            IsBottomZoneCollapsed = _isBottomZoneCollapsed
        };
    }

    /// <inheritdoc />
    public void RestoreLayoutState(LayoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // 保存当前不可移动面板的状态
        var nonMovablePanels = _panels
            .Where(p => !p.IsMovable)
            .ToDictionary(p => p.Id, p => p.Clone());

        _panels.Clear();

        foreach (var panelState in state.Panels)
        {
            var panel = panelState.Clone();

            // 如果是不可移动的面板，恢复其原始状态
            if (nonMovablePanels.TryGetValue(panel.Id, out var original))
            {
                panel.Zone = original.Zone;
                panel.Order = original.Order;
                panel.IsMovable = false;
            }

            _panels.Add(panel);
        }

        // 确保所有原始的不可移动面板都被保留
        foreach (var (id, original) in nonMovablePanels)
        {
            if (!_panels.Any(p => p.Id == id))
            {
                _panels.Add(original);
            }
        }

        _leftZoneWidth = state.LeftZoneWidth;
        _rightZoneWidth = state.RightZoneWidth;
        _bottomZoneHeight = state.BottomZoneHeight;
        _isLeftZoneCollapsed = state.IsLeftZoneCollapsed;
        _isRightZoneCollapsed = state.IsRightZoneCollapsed;
        _isBottomZoneCollapsed = state.IsBottomZoneCollapsed;

        OnLayoutChanged();
    }

    /// <inheritdoc />
    public void SetZoneSize(PanelZone zone, double size)
    {
        switch (zone)
        {
            case PanelZone.Left:
                _leftZoneWidth = size;
                break;
            case PanelZone.Right:
                _rightZoneWidth = size;
                break;
            case PanelZone.Bottom:
                _bottomZoneHeight = size;
                break;
        }

        OnLayoutChanged();
    }

    /// <inheritdoc />
    public double GetZoneSize(PanelZone zone)
    {
        return zone switch
        {
            PanelZone.Left => _leftZoneWidth,
            PanelZone.Right => _rightZoneWidth,
            PanelZone.Bottom => _bottomZoneHeight,
            _ => 0
        };
    }

    /// <inheritdoc />
    public void SetZoneCollapsed(PanelZone zone, bool isCollapsed)
    {
        switch (zone)
        {
            case PanelZone.Left:
                _isLeftZoneCollapsed = isCollapsed;
                break;
            case PanelZone.Right:
                _isRightZoneCollapsed = isCollapsed;
                break;
            case PanelZone.Bottom:
                _isBottomZoneCollapsed = isCollapsed;
                break;
        }

        OnLayoutChanged();
    }

    /// <inheritdoc />
    public bool IsZoneCollapsed(PanelZone zone)
    {
        return zone switch
        {
            PanelZone.Left => _isLeftZoneCollapsed,
            PanelZone.Right => _isRightZoneCollapsed,
            PanelZone.Bottom => _isBottomZoneCollapsed,
            _ => false
        };
    }

    /// <inheritdoc />
    public void ToggleZoneCollapsed(PanelZone zone)
    {
        SetZoneCollapsed(zone, !IsZoneCollapsed(zone));
    }

    /// <inheritdoc />
    public void SetPanelFloating(string panelId, double x, double y, double width, double height)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null && panel.IsMovable)
        {
            panel.IsFloating = true;
            panel.FloatingX = x;
            panel.FloatingY = y;
            panel.FloatingWidth = width;
            panel.FloatingHeight = height;
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public void DockPanel(string panelId, PanelZone zone)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            panel.IsFloating = false;
            panel.Zone = zone;
            
            // 设置为目标区域的最后一个
            var maxOrder = _panels
                .Where(p => p.Zone == zone && p.Id != panelId && !p.IsFloating)
                .Select(p => p.Order)
                .DefaultIfEmpty(-1)
                .Max();
            panel.Order = maxOrder + 1;
            
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public void UpdateFloatingPanelBounds(string panelId, double x, double y, double width, double height)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null && panel.IsFloating)
        {
            panel.FloatingX = x;
            panel.FloatingY = y;
            panel.FloatingWidth = width;
            panel.FloatingHeight = height;
            // 不触发 OnLayoutChanged 避免频繁更新
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PanelInfo> GetFloatingPanels()
    {
        return _panels
            .Where(p => p.IsFloating)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public void TogglePanelVisibility(string panelId)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            panel.IsVisible = !panel.IsVisible;
            OnLayoutChanged();
        }
    }

    /// <inheritdoc />
    public void ActivatePanelInZone(string panelId)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == panelId);
        if (panel == null) return;

        var zone = panel.Zone;
        var zonePanels = _panels.Where(p => p.Zone == zone && !p.IsFloating).ToList();
        
        // Check if this panel is the sole active panel in the zone
        var isSoleActive = panel.IsExpanded && zonePanels.Count(p => p.IsExpanded) == 1;

        // Collapse all panels in the zone
        foreach (var p in zonePanels)
        {
            p.IsExpanded = false;
        }

        // If the panel was not the sole active one, expand it
        if (!isSoleActive)
        {
            panel.IsExpanded = true;
        }

        OnLayoutChanged();
    }

    /// <inheritdoc />
    public PanelInfo? GetActivePanelInZone(PanelZone zone)
    {
        return _panels.FirstOrDefault(p => p.Zone == zone && p.IsExpanded && p.IsVisible && !p.IsFloating);
    }

    private void OnLayoutChanged()
    {
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }
}
