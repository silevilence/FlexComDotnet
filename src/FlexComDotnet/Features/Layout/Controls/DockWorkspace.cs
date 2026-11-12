using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Core.Features.Layout.Services;
using AvalonDock.Themes;
using FlexComDotnet.Services;
using System.Windows.Input;

namespace FlexComDotnet.Features.Layout.Controls;

/// <summary>Owns page instances while AvalonDock owns their layout.</summary>
public sealed class DockWorkspace : UserControl
{
    private readonly Dictionary<string, (LayoutAnchorable Panel, PanelZone Zone, int Order)> _tools = [];
    private readonly Dictionary<string, UIElement> _contents = [];
    private IPanelManager? _catalog;
    private sealed record DocumentPage(LayoutDocument Model, FrameworkElement View, Func<Task<bool>> CanClose, Action? Release);
    private readonly Dictionary<string, DocumentPage> _documents = [];
    private readonly HashSet<string> _closing = [];
    public event EventHandler<Exception>? OperationFailed;
    public event EventHandler<string>? DocumentRestoreRequested;
    public UIElement? CenterContentElement { get; set; }
    public DockingManager Manager { get; } = new() { AllowMixedOrientation = true };

    public DockWorkspace()
    {
        Content = Manager;
        Manager.SetResourceReference(BackgroundProperty, "BackgroundBrush");
        Manager.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        KeyboardNavigation.SetTabNavigation(Manager, KeyboardNavigationMode.Continue);
        Manager.DocumentClosing += async (_, args) =>
        {
            if (args.Document.ContentId is not { } id || !_documents.ContainsKey(id)) return;
            args.Cancel = true;
            await TryCloseDocumentAsync(id);
        };
    }

    public void AddPanel(string id, string title, UIElement content, PanelZone zone, bool isMovable = true, int order = 0)
    {
        if (_tools.ContainsKey(id)) throw new ArgumentException("Duplicate page ID.", nameof(id));
        var panel = new LayoutAnchorable
        {
            ContentId = id, Title = title, Content = content,
            CanClose = false, CanHide = true, CanFloat = true,
            CanAutoHide = true, CanDockAsTabbedDocument = true
        };
        _tools.Add(id, (panel, zone, order));
        _contents.Add(id, content);
        _catalog?.RegisterPanel(new PanelInfo { Id = id, Title = title, Zone = zone, Order = order, IsMovable = true });
    }

    // The legacy catalog remains the settings page's visibility inventory, not a second layout owner.
    public void SetPanelManager(IPanelManager catalog) => _catalog = catalog;

    public void ApplyTheme(ThemeType theme) => Manager.Theme = theme == ThemeType.Dark
        ? new Vs2013DarkTheme() : new Vs2013LightTheme();

    public void InitializeLayout(LayoutState? legacy = null)
    {
        BuildLayout(legacy);
        if (legacy == null) return;
        foreach (var state in legacy.Panels)
        {
            if (!_tools.TryGetValue(state.Id, out var item)) continue;
            var panel = item.Panel;
            if (!state.IsVisible) panel.Hide();
            else if (state.IsFloating)
            {
                var bounds = VisibleBounds(state.FloatingX, state.FloatingY, state.FloatingWidth, state.FloatingHeight);
                panel.FloatingLeft = bounds.X; panel.FloatingTop = bounds.Y;
                panel.FloatingWidth = bounds.Width; panel.FloatingHeight = bounds.Height;
                panel.Float();
            }
        }
        foreach (var pane in Manager.Layout.Descendents().OfType<LayoutAnchorablePane>().ToArray())
        {
            var first = pane.Children.FirstOrDefault();
            if (first == null || first.IsFloating) continue;
            var zone = legacy.Panels.FirstOrDefault(p => p.Id == first.ContentId)?.Zone ?? _tools[first.ContentId!].Zone;
            var expanded = pane.Children.FirstOrDefault(p => legacy.Panels.FirstOrDefault(s => s.Id == p.ContentId)?.IsExpanded != false);
            var collapsed = zone switch
            {
                PanelZone.Left => legacy.IsLeftZoneCollapsed,
                PanelZone.Right => legacy.IsRightZoneCollapsed,
                _ => legacy.IsBottomZoneCollapsed
            };
            // A docked tab group has one active page. Collapse the whole group only when
            // the old zone was collapsed or all of its remaining pages were collapsed.
            if (collapsed || expanded == null) first.ToggleAutoHide();
            else expanded.IsSelected = true;
        }
        Manager.Layout.CollectGarbage();
        SyncCatalog();
    }

    public static Rect VisibleBounds(double x, double y, double width, double height)
    {
        var screen = SystemParameters.WorkArea;
        width = double.IsFinite(width) ? Math.Clamp(width, 240, screen.Width) : 400;
        height = double.IsFinite(height) ? Math.Clamp(height, 160, screen.Height) : 300;
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            x + 80 < SystemParameters.VirtualScreenLeft || y + 32 < SystemParameters.VirtualScreenTop ||
            x > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 ||
            y > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 32)
        { x = screen.Left + 24; y = screen.Top + 24; }
        return new Rect(x, y, width, height);
    }

    private void SyncCatalog()
    {
        foreach (var (id, tool) in _tools) _catalog?.SetPanelVisibility(id, !tool.Panel.IsHidden);
    }

    private void ObservePanels()
    {
        foreach (var tool in _tools.Values)
            tool.Panel.IsVisibleChanged += (_, _) => SyncCatalog();
        SyncCatalog();
    }

    public void ResetLayout() => BuildLayout(null);

    private void BuildLayout(LayoutState? legacy)
    {
        DetachContent();
        foreach (var (id, item) in _tools.ToArray())
        {
            _tools[id] = (new LayoutAnchorable
            {
                ContentId = id, Title = item.Panel.Title, Content = _contents[id],
                CanClose = false, CanHide = true, CanFloat = true,
                CanAutoHide = true, CanDockAsTabbedDocument = true
            }, item.Zone, item.Order);
        }
        var documents = new LayoutDocumentPane();
        if (CenterContentElement != null)
            documents.Children.Add(new LayoutDocument { ContentId = "communication", Title = "收发", Content = CenterContentElement, CanClose = false });
        foreach (var page in _documents.Values)
        {
            page.Model.Content = page.View;
            documents.Children.Add(page.Model);
        }
        var horizontal = new LayoutPanel { Orientation = Orientation.Horizontal };
        AddZone(horizontal, PanelZone.Left, legacy?.LeftZoneWidth ?? 250, legacy);
        horizontal.Children.Add(documents);
        AddZone(horizontal, PanelZone.Right, legacy?.RightZoneWidth ?? 300, legacy);
        var root = new LayoutPanel { Orientation = Orientation.Vertical };
        root.Children.Add(horizontal);
        AddZone(root, PanelZone.Bottom, legacy?.BottomZoneHeight ?? 220, legacy);
        Manager.Layout = new LayoutRoot { RootPanel = root };
        ObservePanels();
    }

    private void AddZone(LayoutPanel parent, PanelZone zone, double size, LayoutState? legacy)
    {
        PanelInfo? State(LayoutAnchorable panel) => legacy?.Panels.FirstOrDefault(p => p.Id == panel.ContentId);
        var items = _tools.Values.Where(t => (State(t.Panel)?.Zone ?? t.Zone) == zone)
            .OrderBy(t => State(t.Panel)?.Order ?? t.Order).ToArray();
        size = double.IsFinite(size) ? Math.Clamp(size, zone == PanelZone.Bottom ? 100 : 150, 800) : 250;
        if (items.Length == 0) return;
        var pane = new LayoutAnchorablePane { DockMinWidth = 150, DockMinHeight = 100 };
        if (zone == PanelZone.Bottom) pane.DockHeight = new GridLength(size);
        else pane.DockWidth = new GridLength(size);
        foreach (var item in items) pane.Children.Add(item.Panel);
        parent.Children.Add(pane);
    }

    private void DetachContent()
    {
        foreach (var page in Manager.Layout.Descendents().OfType<LayoutContent>().ToArray()) page.Content = null;
    }

    public string SaveLayout()
    {
        using var writer = new StringWriter();
        new XmlLayoutSerializer(Manager).Serialize(writer);
        return writer.ToString();
    }

    public bool RestoreLayout(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            if (document.Root?.Name.LocalName != "LayoutRoot") throw new InvalidDataException("Unknown layout format.");
            var version = (string?)document.Root.Attribute("FlexComVersion");
            if (version != null && version != "1") throw new InvalidDataException("Unsupported layout version.");
            var ids = document.Descendants().Attributes("ContentId").Select(a => a.Value).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new InvalidDataException("Duplicate page IDs.");
            foreach (var id in ids.Where(id => id != "communication" && !_contents.ContainsKey(id) && !_documents.ContainsKey(id)))
                DocumentRestoreRequested?.Invoke(this, id);
            foreach (var element in document.Descendants().Where(e => e.Attribute("FloatingLeft") != null))
            {
                double Read(string key) => double.TryParse((string?)element.Attribute(key),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
                var bounds = VisibleBounds(Read("FloatingLeft"), Read("FloatingTop"), Read("FloatingWidth"), Read("FloatingHeight"));
                element.SetAttributeValue("FloatingLeft", bounds.X); element.SetAttributeValue("FloatingTop", bounds.Y);
                element.SetAttributeValue("FloatingWidth", bounds.Width); element.SetAttributeValue("FloatingHeight", bounds.Height);
            }
            DetachContent();
            var serializer = new XmlLayoutSerializer(Manager);
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                var id = args.Model.ContentId;
                args.Content = id == "communication" ? CenterContentElement : _contents.GetValueOrDefault(id ?? "");
                if (id != null && _documents.TryGetValue(id, out var page)) args.Content = page.View;
                args.Cancel = args.Content == null;
            };
            using var reader = new StringReader(document.ToString());
            serializer.Deserialize(reader);
            var restoredIds = Manager.Layout.Descendents().OfType<LayoutContent>().Select(p => p.ContentId).ToHashSet();
            foreach (var (id, previous) in _tools.ToArray())
            {
                if (restoredIds.Contains(id)) continue;
                var panel = new LayoutAnchorable { ContentId = id, Title = previous.Panel.Title, Content = _contents[id], CanClose = false };
                Manager.Layout.Hidden.Add(panel);
                _tools[id] = (panel, previous.Zone, previous.Order);
            }
            if (CenterContentElement != null && !restoredIds.Contains("communication"))
                GetDocumentPane().Children.Add(new LayoutDocument { ContentId = "communication", Title = "收发", Content = CenterContentElement, CanClose = false });
            foreach (var page in _documents.Values)
                if (!restoredIds.Contains(page.Model.ContentId))
                {
                    page.Model.Content = page.View;
                    GetDocumentPane().Children.Add(page.Model);
                }
            foreach (var panel in Manager.Layout.Descendents().OfType<LayoutAnchorable>())
            {
                if (panel.ContentId != null && _tools.TryGetValue(panel.ContentId, out var previous))
                {
                    panel.CanClose = false;
                    panel.CanHide = true;
                    panel.CanFloat = true;
                    panel.CanAutoHide = true;
                    panel.CanDockAsTabbedDocument = true;
                    _tools[panel.ContentId] = (panel, previous.Zone, previous.Order);
                }
            }
            foreach (var page in Manager.Layout.Descendents().OfType<LayoutDocument>())
            {
                if (page.ContentId == "communication") page.CanClose = false;
                else if (page.ContentId != null && _documents.TryGetValue(page.ContentId, out var entry))
                    _documents[page.ContentId] = entry with { Model = page };
            }
            ObservePanels();
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.TraceWarning("Layout restore failed: {0}", error.Message);
            ResetLayout();
            return false;
        }
    }

    public void HidePanel(string id)
    {
        if (_tools.TryGetValue(id, out var item)) item.Panel.Hide();
    }

    public void ShowPanel(string id)
    {
        if (!_tools.TryGetValue(id, out var item)) return;
        if (item.Panel.IsHidden) item.Panel.Show();
        if (item.Panel.Parent == null) item.Panel.AddToLayout(Manager, AnchorableShowStrategy.Right);
        item.Panel.IsSelected = true;
        item.Panel.IsActive = true;
    }

    public void TogglePanelVisibility(string id)
    {
        if (!_tools.TryGetValue(id, out var item)) return;
        if (item.Panel.IsHidden) ShowPanel(id);
        else HidePanel(id);
    }

    public FrameworkElement OpenDocument(string id, string title, Func<FrameworkElement> create,
        Func<Task<bool>>? canClose = null, Action? release = null)
    {
        if (!_documents.TryGetValue(id, out var page))
        {
            var view = create();
            FocusManager.SetIsFocusScope(view, true);
            var model = new LayoutDocument { ContentId = id, Title = title, Content = view, CanClose = true, CanFloat = true };
            page = new DocumentPage(model, view, canClose ?? (() => Task.FromResult(true)), release);
            _documents.Add(id, page);
            GetDocumentPane().Children.Add(model);
        }
        page.Model.IsSelected = true;
        page.Model.IsActive = true;
        page.View.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        return page.View;
    }

    private LayoutDocumentPane GetDocumentPane()
    {
        var pane = Manager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (pane != null) return pane;
        pane = new LayoutDocumentPane();
        Manager.Layout.RootPanel.Children.Add(pane);
        return pane;
    }

    public async Task<bool> TryCloseDocumentAsync(string id)
    {
        if (!_documents.TryGetValue(id, out var page) || !_closing.Add(id)) return false;
        try
        {
            if (!await page.CanClose()) { RestoreFocus(page.Model); return false; }
            page.Model.Parent?.RemoveChild(page.Model);
            _documents.Remove(id);
            page.Model.Content = null;
            page.Release?.Invoke();
            Manager.Layout.CollectGarbage();
            var next = Manager.Layout.Descendents().OfType<LayoutContent>()
                .FirstOrDefault(p => p.Content != null && (p is not LayoutAnchorable tool || !tool.IsHidden));
            if (next != null) RestoreFocus(next);
            return true;
        }
        catch (Exception error)
        {
            OperationFailed?.Invoke(this, error);
            return false;
        }
        finally { _closing.Remove(id); }
    }

    public async Task<bool> CanCloseDocumentsAsync()
    {
        foreach (var page in _documents.Values.ToArray())
            if (!await page.CanClose()) { RestoreFocus(page.Model); return false; }
        return true;
    }

    public void ReleaseDocuments()
    {
        foreach (var page in _documents.Values) page.Release?.Invoke();
        _documents.Clear();
    }

    public void ActivateNextPage(int direction)
    {
        var pages = Manager.Layout.Descendents().OfType<LayoutContent>()
            .Where(p => p.Content != null && (p is not LayoutAnchorable tool || !tool.IsHidden)).ToArray();
        if (pages.Length == 0) return;
        var index = Array.FindIndex(pages, p => p.IsActive);
        var next = pages[(index + direction + pages.Length) % pages.Length];
        RestoreFocus(next);
    }

    private static void RestoreFocus(LayoutContent page)
    {
        page.IsSelected = true;
        page.IsActive = true;
        if (page.Content is not FrameworkElement view) return;
        if (FocusManager.GetFocusedElement(view) is { } previous) Keyboard.Focus(previous);
        else view.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }
}
