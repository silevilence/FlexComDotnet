using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Layout;

public class PanelManagerTests
{
    private readonly PanelManager _sut;

    public PanelManagerTests()
    {
        _sut = new PanelManager();
    }

    #region RegisterPanel Tests

    [Fact]
    public void RegisterPanel_ShouldAddPanelToList()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test Panel", PanelZone.Left);

        // Act
        _sut.RegisterPanel(panel);

        // Assert
        _sut.Panels.Should().ContainSingle(p => p.Id == "test-panel");
    }

    [Fact]
    public void RegisterPanel_WithDuplicateId_ShouldPreserveExistingState()
    {
        // Arrange
        var panel1 = CreateTestPanel("test-panel", "Panel 1", PanelZone.Left);
        panel1.IsVisible = false;
        panel1.IsFloating = true;
        var panel2 = CreateTestPanel("test-panel", "Panel 2", PanelZone.Right);

        // Act
        _sut.RegisterPanel(panel1);
        _sut.RegisterPanel(panel2);

        // Assert - 保留现有状态，只更新标题
        _sut.Panels.Should().ContainSingle();
        _sut.Panels.First().Title.Should().Be("Panel 2");
        _sut.Panels.First().Zone.Should().Be(PanelZone.Left);  // 保留原始 Zone
        _sut.Panels.First().IsVisible.Should().BeFalse();       // 保留 IsVisible
        _sut.Panels.First().IsFloating.Should().BeTrue();       // 保留 IsFloating
    }

    [Fact]
    public void RegisterPanel_ShouldRaiseLayoutChangedEvent()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test Panel", PanelZone.Left);
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.RegisterPanel(panel);

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion

    #region RemovePanel Tests

    [Fact]
    public void RemovePanel_ShouldRemovePanelFromList()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test Panel", PanelZone.Left);
        _sut.RegisterPanel(panel);

        // Act
        _sut.RemovePanel("test-panel");

        // Assert
        _sut.Panels.Should().BeEmpty();
    }

    [Fact]
    public void RemovePanel_WithNonExistentId_ShouldDoNothing()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test Panel", PanelZone.Left);
        _sut.RegisterPanel(panel);

        // Act
        _sut.RemovePanel("non-existent");

        // Assert
        _sut.Panels.Should().ContainSingle();
    }

    [Fact]
    public void RemovePanel_ShouldRaiseLayoutChangedEvent()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test Panel", PanelZone.Left);
        _sut.RegisterPanel(panel);
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.RemovePanel("test-panel");

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion

    #region GetPanelsInZone Tests

    [Fact]
    public void GetPanelsInZone_ShouldReturnOnlyPanelsInSpecifiedZone()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("left-1", "Left 1", PanelZone.Left, 0));
        _sut.RegisterPanel(CreateTestPanel("left-2", "Left 2", PanelZone.Left, 1));
        _sut.RegisterPanel(CreateTestPanel("right-1", "Right 1", PanelZone.Right, 0));

        // Act
        var leftPanels = _sut.GetPanelsInZone(PanelZone.Left);

        // Assert
        leftPanels.Should().HaveCount(2);
        leftPanels.Should().OnlyContain(p => p.Zone == PanelZone.Left);
    }

    [Fact]
    public void GetPanelsInZone_ShouldReturnPanelsOrderedByOrder()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("left-2", "Left 2", PanelZone.Left, 2));
        _sut.RegisterPanel(CreateTestPanel("left-0", "Left 0", PanelZone.Left, 0));
        _sut.RegisterPanel(CreateTestPanel("left-1", "Left 1", PanelZone.Left, 1));

        // Act
        var leftPanels = _sut.GetPanelsInZone(PanelZone.Left);

        // Assert
        leftPanels.Select(p => p.Id).Should().ContainInOrder("left-0", "left-1", "left-2");
    }

    [Fact]
    public void GetPanelsInZone_ShouldReturnEmptyListForEmptyZone()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("left-1", "Left 1", PanelZone.Left, 0));

        // Act
        var bottomPanels = _sut.GetPanelsInZone(PanelZone.Bottom);

        // Assert
        bottomPanels.Should().BeEmpty();
    }

    #endregion

    #region MovePanel Tests

    [Fact]
    public void MovePanel_ShouldChangePanelZone()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("test-panel", "Test", PanelZone.Left, 0));

        // Act
        var result = _sut.MovePanel("test-panel", PanelZone.Right);

        // Assert
        result.Should().BeTrue();
        _sut.GetPanel("test-panel")!.Zone.Should().Be(PanelZone.Right);
    }

    [Fact]
    public void MovePanel_WithNonMovablePanel_ShouldReturnFalse()
    {
        // Arrange
        var panel = CreateTestPanel("fixed-panel", "Fixed", PanelZone.Left, 0);
        panel.IsMovable = false;
        _sut.RegisterPanel(panel);

        // Act
        var result = _sut.MovePanel("fixed-panel", PanelZone.Right);

        // Assert
        result.Should().BeFalse();
        _sut.GetPanel("fixed-panel")!.Zone.Should().Be(PanelZone.Left);
    }

    [Fact]
    public void MovePanel_WithNonExistentPanel_ShouldReturnFalse()
    {
        // Act
        var result = _sut.MovePanel("non-existent", PanelZone.Right);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MovePanel_WithTargetOrder_ShouldSetSpecifiedOrder()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("test-panel", "Test", PanelZone.Left, 0));

        // Act
        _sut.MovePanel("test-panel", PanelZone.Right, 5);

        // Assert
        _sut.GetPanel("test-panel")!.Order.Should().Be(5);
    }

    [Fact]
    public void MovePanel_WithoutTargetOrder_ShouldAppendToEnd()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("right-1", "Right 1", PanelZone.Right, 0));
        _sut.RegisterPanel(CreateTestPanel("right-2", "Right 2", PanelZone.Right, 1));
        _sut.RegisterPanel(CreateTestPanel("test-panel", "Test", PanelZone.Left, 0));

        // Act
        _sut.MovePanel("test-panel", PanelZone.Right);

        // Assert
        _sut.GetPanel("test-panel")!.Order.Should().Be(2);
    }

    [Fact]
    public void MovePanel_ShouldRaiseLayoutChangedEvent()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("test-panel", "Test", PanelZone.Left, 0));
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.MovePanel("test-panel", PanelZone.Right);

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion

    #region SetPanelExpanded Tests

    [Fact]
    public void SetPanelExpanded_ShouldUpdateExpandedState()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        panel.IsExpanded = true;
        _sut.RegisterPanel(panel);

        // Act
        _sut.SetPanelExpanded("test-panel", false);

        // Assert
        _sut.GetPanel("test-panel")!.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void TogglePanelExpanded_ShouldToggleState()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        panel.IsExpanded = true;
        _sut.RegisterPanel(panel);

        // Act
        _sut.TogglePanelExpanded("test-panel");

        // Assert
        _sut.GetPanel("test-panel")!.IsExpanded.Should().BeFalse();

        // Act again
        _sut.TogglePanelExpanded("test-panel");

        // Assert
        _sut.GetPanel("test-panel")!.IsExpanded.Should().BeTrue();
    }

    #endregion

    #region SetPanelVisibility Tests

    [Fact]
    public void SetPanelVisibility_ShouldUpdateVisibleState()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        panel.IsVisible = true;
        _sut.RegisterPanel(panel);

        // Act
        _sut.SetPanelVisibility("test-panel", false);

        // Assert
        _sut.GetPanel("test-panel")!.IsVisible.Should().BeFalse();
    }

    #endregion

    #region Zone Management Tests

    [Fact]
    public void SetZoneSize_ShouldUpdateZoneSize()
    {
        // Act
        _sut.SetZoneSize(PanelZone.Left, 350);

        // Assert
        _sut.GetZoneSize(PanelZone.Left).Should().Be(350);
    }

    [Fact]
    public void SetZoneCollapsed_ShouldUpdateCollapsedState()
    {
        // Act
        _sut.SetZoneCollapsed(PanelZone.Left, true);

        // Assert
        _sut.IsZoneCollapsed(PanelZone.Left).Should().BeTrue();
    }

    [Fact]
    public void ToggleZoneCollapsed_ShouldToggleState()
    {
        // Arrange
        _sut.SetZoneCollapsed(PanelZone.Left, false);

        // Act
        _sut.ToggleZoneCollapsed(PanelZone.Left);

        // Assert
        _sut.IsZoneCollapsed(PanelZone.Left).Should().BeTrue();
    }

    #endregion

    #region LayoutState Tests

    [Fact]
    public void GetLayoutState_ShouldReturnCurrentState()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("panel-1", "Panel 1", PanelZone.Left, 0));
        _sut.RegisterPanel(CreateTestPanel("panel-2", "Panel 2", PanelZone.Right, 0));
        _sut.SetZoneSize(PanelZone.Left, 400);
        _sut.SetZoneCollapsed(PanelZone.Right, true);

        // Act
        var state = _sut.GetLayoutState();

        // Assert
        state.Panels.Should().HaveCount(2);
        state.LeftZoneWidth.Should().Be(400);
        state.IsRightZoneCollapsed.Should().BeTrue();
    }

    [Fact]
    public void RestoreLayoutState_ShouldRestoreState()
    {
        // Arrange
        var state = new LayoutState
        {
            Panels =
            [
                new PanelInfo { Id = "panel-1", Title = "Panel 1", Zone = PanelZone.Left, Order = 0 },
                new PanelInfo { Id = "panel-2", Title = "Panel 2", Zone = PanelZone.Right, Order = 0 }
            ],
            LeftZoneWidth = 350,
            RightZoneWidth = 250,
            BottomZoneHeight = 150,
            IsLeftZoneCollapsed = false,
            IsRightZoneCollapsed = true,
            IsBottomZoneCollapsed = false
        };

        // Act
        _sut.RestoreLayoutState(state);

        // Assert
        _sut.Panels.Should().HaveCount(2);
        _sut.GetZoneSize(PanelZone.Left).Should().Be(350);
        _sut.GetZoneSize(PanelZone.Right).Should().Be(250);
        _sut.GetZoneSize(PanelZone.Bottom).Should().Be(150);
        _sut.IsZoneCollapsed(PanelZone.Right).Should().BeTrue();
    }

    [Fact]
    public void RestoreLayoutState_ShouldMergeWithExistingNonMovablePanels()
    {
        // Arrange
        var fixedPanel = CreateTestPanel("fixed-panel", "Fixed Panel", PanelZone.Left, 0);
        fixedPanel.IsMovable = false;
        _sut.RegisterPanel(fixedPanel);

        var state = new LayoutState
        {
            Panels =
            [
                new PanelInfo { Id = "fixed-panel", Title = "Modified", Zone = PanelZone.Right, Order = 5 },
                new PanelInfo { Id = "new-panel", Title = "New Panel", Zone = PanelZone.Right, Order = 0 }
            ]
        };

        // Act
        _sut.RestoreLayoutState(state);

        // Assert
        // 固定面板应保持原始区域
        var restoredFixed = _sut.GetPanel("fixed-panel");
        restoredFixed!.Zone.Should().Be(PanelZone.Left);
        restoredFixed.IsMovable.Should().BeFalse();
    }

    #endregion

    #region Floating Panel Tests

    [Fact]
    public void SetPanelFloating_ShouldSetFloatingState()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);

        // Act
        _sut.SetPanelFloating("test-panel", 100, 200, 300, 400);

        // Assert
        var floatingPanel = _sut.GetPanel("test-panel");
        floatingPanel!.IsFloating.Should().BeTrue();
        floatingPanel.FloatingX.Should().Be(100);
        floatingPanel.FloatingY.Should().Be(200);
        floatingPanel.FloatingWidth.Should().Be(300);
        floatingPanel.FloatingHeight.Should().Be(400);
    }

    [Fact]
    public void SetPanelFloating_WithNonMovablePanel_ShouldNotFloat()
    {
        // Arrange
        var panel = CreateTestPanel("fixed-panel", "Fixed", PanelZone.Left, 0);
        panel.IsMovable = false;
        _sut.RegisterPanel(panel);

        // Act
        _sut.SetPanelFloating("fixed-panel", 100, 200, 300, 400);

        // Assert
        _sut.GetPanel("fixed-panel")!.IsFloating.Should().BeFalse();
    }

    [Fact]
    public void SetPanelFloating_ShouldRaiseLayoutChangedEvent()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.SetPanelFloating("test-panel", 100, 200, 300, 400);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void DockPanel_ShouldSetPanelBackToDocked()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);
        _sut.SetPanelFloating("test-panel", 100, 200, 300, 400);

        // Act
        _sut.DockPanel("test-panel", PanelZone.Right);

        // Assert
        var dockedPanel = _sut.GetPanel("test-panel");
        dockedPanel!.IsFloating.Should().BeFalse();
        dockedPanel.Zone.Should().Be(PanelZone.Right);
    }

    [Fact]
    public void DockPanel_ShouldAppendToEndOfTargetZone()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("right-1", "Right 1", PanelZone.Right, 0));
        _sut.RegisterPanel(CreateTestPanel("right-2", "Right 2", PanelZone.Right, 1));
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);
        _sut.SetPanelFloating("test-panel", 100, 200, 300, 400);

        // Act
        _sut.DockPanel("test-panel", PanelZone.Right);

        // Assert
        _sut.GetPanel("test-panel")!.Order.Should().Be(2);
    }

    [Fact]
    public void DockPanel_ShouldRaiseLayoutChangedEvent()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);
        _sut.SetPanelFloating("test-panel", 100, 200, 300, 400);
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.DockPanel("test-panel", PanelZone.Right);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void UpdateFloatingPanelBounds_ShouldUpdateBounds()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);
        _sut.SetPanelFloating("test-panel", 100, 200, 300, 400);

        // Act
        _sut.UpdateFloatingPanelBounds("test-panel", 150, 250, 350, 450);

        // Assert
        var floatingPanel = _sut.GetPanel("test-panel");
        floatingPanel!.FloatingX.Should().Be(150);
        floatingPanel.FloatingY.Should().Be(250);
        floatingPanel.FloatingWidth.Should().Be(350);
        floatingPanel.FloatingHeight.Should().Be(450);
    }

    [Fact]
    public void UpdateFloatingPanelBounds_WithDockedPanel_ShouldNotUpdateBounds()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);

        // Act
        _sut.UpdateFloatingPanelBounds("test-panel", 150, 250, 350, 450);

        // Assert
        var dockedPanel = _sut.GetPanel("test-panel");
        dockedPanel!.FloatingX.Should().Be(0); // 默认值
    }

    [Fact]
    public void GetFloatingPanels_ShouldReturnOnlyFloatingPanels()
    {
        // Arrange
        var panel1 = CreateTestPanel("panel-1", "Panel 1", PanelZone.Left, 0);
        var panel2 = CreateTestPanel("panel-2", "Panel 2", PanelZone.Right, 0);
        var panel3 = CreateTestPanel("panel-3", "Panel 3", PanelZone.Bottom, 0);
        _sut.RegisterPanel(panel1);
        _sut.RegisterPanel(panel2);
        _sut.RegisterPanel(panel3);
        _sut.SetPanelFloating("panel-1", 100, 200, 300, 400);
        _sut.SetPanelFloating("panel-3", 150, 250, 350, 450);

        // Act
        var floatingPanels = _sut.GetFloatingPanels();

        // Assert
        floatingPanels.Should().HaveCount(2);
        floatingPanels.Should().Contain(p => p.Id == "panel-1");
        floatingPanels.Should().Contain(p => p.Id == "panel-3");
        floatingPanels.Should().NotContain(p => p.Id == "panel-2");
    }

    [Fact]
    public void GetFloatingPanels_WithNoFloatingPanels_ShouldReturnEmptyList()
    {
        // Arrange
        _sut.RegisterPanel(CreateTestPanel("panel-1", "Panel 1", PanelZone.Left, 0));
        _sut.RegisterPanel(CreateTestPanel("panel-2", "Panel 2", PanelZone.Right, 0));

        // Act
        var floatingPanels = _sut.GetFloatingPanels();

        // Assert
        floatingPanels.Should().BeEmpty();
    }

    #endregion

    #region Panel Visibility Tests

    [Fact]
    public void TogglePanelVisibility_ShouldToggleState()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        panel.IsVisible = true;
        _sut.RegisterPanel(panel);

        // Act
        _sut.TogglePanelVisibility("test-panel");

        // Assert
        _sut.GetPanel("test-panel")!.IsVisible.Should().BeFalse();

        // Act again
        _sut.TogglePanelVisibility("test-panel");

        // Assert
        _sut.GetPanel("test-panel")!.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void TogglePanelVisibility_ShouldRaiseLayoutChangedEvent()
    {
        // Arrange
        var panel = CreateTestPanel("test-panel", "Test", PanelZone.Left, 0);
        _sut.RegisterPanel(panel);
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.TogglePanelVisibility("test-panel");

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void TogglePanelVisibility_WithNonExistentPanel_ShouldDoNothing()
    {
        // Arrange
        var eventRaised = false;
        _sut.LayoutChanged += (_, _) => eventRaised = true;

        // Act
        _sut.TogglePanelVisibility("non-existent");

        // Assert
        eventRaised.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static PanelInfo CreateTestPanel(string id, string title, PanelZone zone, int order = 0)
    {
        return new PanelInfo
        {
            Id = id,
            Title = title,
            Zone = zone,
            Order = order,
            IsExpanded = true,
            IsMovable = true,
            IsVisible = true
        };
    }

    #endregion
}
