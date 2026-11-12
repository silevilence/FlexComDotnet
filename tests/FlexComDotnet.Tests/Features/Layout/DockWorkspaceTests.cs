using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using AvalonDock.Layout;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Features.Layout.Controls;
using FluentAssertions;
using FlexComDotnet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FlexComDotnet.Tests.Features.Layout;

public class DockWorkspaceTests
{
    [Fact]
    public void NestedSplits_ShouldAllowBothOrientationsAndRoundTrip() => RunSta(() =>
    {
        var workspace = new DockWorkspace { CenterContentElement = new TextBox() };
        workspace.ResetLayout();
        workspace.OpenDocument("one", "one", () => new TextBox());
        workspace.Measure(new System.Windows.Size(1000, 700));
        workspace.Arrange(new System.Windows.Rect(0, 0, 1000, 700));
        workspace.UpdateLayout();
        var one = workspace.Manager.Layout.Descendents().OfType<LayoutDocument>().Single(p => p.ContentId == "one");
        var commands = workspace.Manager.GetLayoutItemFromModel(one);
        commands.NewVerticalTabGroupCommand.CanExecute(null).Should().BeTrue();
        commands.NewVerticalTabGroupCommand.Execute(null);
        workspace.OpenDocument("two", "two", () => new TextBox());
        workspace.UpdateLayout();
        var two = workspace.Manager.Layout.Descendents().OfType<LayoutDocument>().Single(p => p.ContentId == "two");
        var nested = workspace.Manager.GetLayoutItemFromModel(two);
        nested.NewHorizontalTabGroupCommand.CanExecute(null).Should().BeTrue();
        nested.NewHorizontalTabGroupCommand.Execute(null);
        var layout = workspace.SaveLayout();
        workspace.ResetLayout();
        workspace.RestoreLayout(layout).Should().BeTrue();
        workspace.Manager.Layout.Descendents().OfType<LayoutDocumentPane>().Count(p => p.Children.Count > 0).Should().Be(3);
        workspace.Manager.Layout.Descendents().OfType<LayoutDocumentPaneGroup>().Select(p => p.Orientation)
            .Concat(workspace.Manager.Layout.Descendents().OfType<LayoutPanel>().Select(p => p.Orientation))
            .Should().Contain(Orientation.Horizontal).And.Contain(Orientation.Vertical);
    });

    [Fact]
    public void RestoreDocuments_ShouldResolveSavedIdsAndSkipUnavailablePages() => RunSta(() =>
    {
        var first = new DockWorkspace { CenterContentElement = new TextBox() };
        first.ResetLayout();
        first.OpenDocument("script:one", "one", () => new TextBox { Text = "private content" });
        first.OpenDocument("removed-page", "old", () => new TextBox());
        var layout = first.SaveLayout();
        layout.Should().NotContain("private content");
        var restored = new DockWorkspace { CenterContentElement = new TextBox() };
        restored.DocumentRestoreRequested += (_, id) =>
        {
            if (id == "script:one") restored.OpenDocument(id, "one", () => new TextBox { Text = "loaded from disk" });
        };
        restored.RestoreLayout(layout).Should().BeTrue();
        var documents = restored.Manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
        documents.Select(p => p.ContentId).Should().BeEquivalentTo("communication", "script:one");
        ((TextBox)documents.Single(p => p.ContentId == "script:one").Content).Text.Should().Be("loaded from disk");
    });

    [Fact]
    public void IncomingFrames_ShouldRemainExactlyOnceAcrossRepeatedReparenting() => RunSta(() =>
    {
        var serial = new Moq.Mock<FlexComDotnet.Core.Features.Serial.Services.ISerialPortService>();
        var config = new Moq.Mock<FlexComDotnet.Core.Features.Serial.Services.IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new FlexComDotnet.Core.Features.Serial.Models.AppConfig());
        using var vm = new FlexComDotnet.Core.Features.Serial.ViewModels.SerialCommunicationViewModel(serial.Object, config.Object,
            Moq.Mock.Of<FlexComDotnet.Core.Features.Serial.Services.ILogSaveService>());
        vm.SendText = "unsent draft";
        var view = new FlexComDotnet.Features.Serial.Views.SerialCommunicationView { DataContext = vm };
        var workspace = new DockWorkspace();
        workspace.AddPanel("serial", "收发", view, PanelZone.Left);
        workspace.ResetLayout();
        var saved = workspace.SaveLayout();
        for (var i = 0; i < 100; i++)
        {
            workspace.HidePanel("serial");
            serial.Raise(s => s.FrameReceived += null, serial.Object, new byte[] { (byte)i, 0x55 });
            workspace.ShowPanel("serial");
            if (i % 10 == 0) workspace.RestoreLayout(saved).Should().BeTrue();
        }
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        vm.RxBytes.Should().Be(200);
        vm.DisplayRecords.Should().HaveCount(100);
        vm.SendText.Should().Be("unsent draft");
        serial.Verify(s => s.Close(), Moq.Times.Never);
    });

    [Fact]
    public void LegacyLayout_ShouldMoveWholeGroupsAndKeepExpandedPeerVisible() => RunSta(() =>
    {
        var workspace = new DockWorkspace();
        workspace.AddPanel("one", "一", new TextBox(), PanelZone.Left);
        workspace.AddPanel("two", "二", new TextBox(), PanelZone.Right);
        workspace.InitializeLayout(new LayoutState
        {
            RightZoneWidth = 350,
            Panels =
            [
                new PanelInfo { Id = "one", Zone = PanelZone.Right, Order = 1, IsExpanded = false },
                new PanelInfo { Id = "two", Zone = PanelZone.Right, Order = 0, IsExpanded = true }
            ]
        });
        var pane = workspace.Manager.Layout.Descendents().OfType<LayoutAnchorablePane>().Single();
        pane.Children.Select(p => p.ContentId).Should().Equal("two", "one");
        pane.Children.Should().OnlyContain(p => !p.IsAutoHidden);
        pane.SelectedContent!.ContentId.Should().Be("two");
        pane.DockWidth.Value.Should().Be(350);
        workspace.ResetLayout();
        workspace.Manager.Layout.Descendents().OfType<LayoutAnchorablePane>().Should().HaveCount(2);
    });

    [Theory]
    [InlineData("<LayoutRoot FlexComVersion=\"99\" />")]
    [InlineData("<LayoutRoot><LayoutDocument ContentId=\"x\"/><LayoutDocument ContentId=\"x\"/></LayoutRoot>")]
    public void InvalidLayout_ShouldFallBackWithoutLosingInput(string xml) => RunSta(() =>
    {
        var input = new TextBox { Text = "unsent" };
        var workspace = new DockWorkspace { CenterContentElement = input };
        workspace.ResetLayout();
        workspace.RestoreLayout(xml).Should().BeFalse();
        workspace.Manager.Layout.Descendents().OfType<LayoutDocument>().Single().Content.Should().BeSameAs(input);
        input.Text.Should().Be("unsent");
        DockWorkspace.VisibleBounds(double.NaN, double.PositiveInfinity, double.NaN, 0)
            .IntersectsWith(System.Windows.SystemParameters.WorkArea).Should().BeTrue();
    });

    [Fact]
    public void RestoreOlderLayout_ShouldMakeNewPagesAvailableAndKeepMainPage() => RunSta(() =>
    {
        var old = new DockWorkspace();
        old.AddPanel("log", "日志", new TextBox(), PanelZone.Bottom);
        old.ResetLayout();
        var saved = old.SaveLayout();
        var workspace = new DockWorkspace { CenterContentElement = new TextBox() };
        workspace.AddPanel("log", "日志", new TextBox(), PanelZone.Bottom);
        workspace.AddPanel("new", "新增工具", new TextBox { Text = "retained" }, PanelZone.Right);
        workspace.ResetLayout();
        workspace.RestoreLayout(saved).Should().BeTrue();
        workspace.ShowPanel("new");
        workspace.Manager.Layout.Descendents().OfType<LayoutContent>().Select(p => p.ContentId)
            .Should().Contain(["communication", "new", "log"]);
        workspace.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == "new")
            .Content.Should().BeOfType<TextBox>().Which.Text.Should().Be("retained");
    });

    [Fact]
    public void Documents_ShouldBeSingletonAndHonorCloseCancellation() => RunSta(() =>
    {
        var workspace = new DockWorkspace();
        workspace.ResetLayout();
        var creations = 0;
        var allowClose = false;
        var released = false;
        System.Windows.FrameworkElement Create() { creations++; return new TextBox { Text = "draft" }; }
        workspace.OpenDocument("script:one", "脚本", Create, () => Task.FromResult(allowClose), () => released = true);
        workspace.OpenDocument("script:one", "脚本", Create);
        creations.Should().Be(1);
        workspace.TryCloseDocumentAsync("script:one").GetAwaiter().GetResult().Should().BeFalse();
        released.Should().BeFalse();
        workspace.ResetLayout();
        workspace.Manager.Layout.Descendents().OfType<LayoutDocument>().Single().Content.Should().BeOfType<TextBox>();
        allowClose = true;
        workspace.TryCloseDocumentAsync("script:one").GetAwaiter().GetResult().Should().BeTrue();
        released.Should().BeTrue();
    });

    [Fact]
    public void LegacyLayout_ShouldKeepVisibilityAndAllowConnectionPanelToMove() => RunSta(() =>
    {
        var catalog = new FlexComDotnet.Core.Features.Layout.Services.PanelManager();
        var workspace = new DockWorkspace();
        workspace.SetPanelManager(catalog);
        workspace.AddPanel("connection-config", "连接配置", new TextBox(), PanelZone.Left, false);
        workspace.InitializeLayout(new LayoutState
        {
            LeftZoneWidth = 310,
            Panels = [new PanelInfo { Id = "connection-config", Zone = PanelZone.Right, IsVisible = false, IsMovable = false }]
        });
        catalog.GetPanel("connection-config")!.IsVisible.Should().BeFalse();
        workspace.ShowPanel("connection-config");
        catalog.GetPanel("connection-config")!.IsVisible.Should().BeTrue();
        var page = workspace.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Single();
        page.CanFloat.Should().BeTrue();
        page.Content.Should().NotBeNull();
    });

    [Fact]
    public void Prototype_ShouldHostRealPagesAndRetainTheirViewModelsAcrossLayoutRestore() => RunSta(() =>
    {
        // No connections or capture sessions are started by this test.
        using var services = new ServiceCollection().AddAppServices(Path.Combine(Path.GetTempPath(), "FlexComDockPrototype", Guid.NewGuid().ToString("N"))).BuildServiceProvider();
        var communication = new FlexComDotnet.Features.Serial.Views.SerialCommunicationView
        {
            DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.Serial.ViewModels.SerialCommunicationViewModel>()
        };
        var connection = new FlexComDotnet.Features.Network.Views.ConnectionConfigView
        {
            DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.Network.ViewModels.ConnectionConfigViewModel>()
        };
        var log = new FlexComDotnet.Features.Logging.Views.LogPanelView(new(
            services.GetRequiredService<FlexComDotnet.Core.Features.Logging.Services.ILoggingService>()));
        var plot = new FlexComDotnet.Features.Visualization.Views.DataVisualizationView(
            services.GetRequiredService<FlexComDotnet.Core.Features.Visualization.ViewModels.DataVisualizationViewModel>());
        var workspace = new DockWorkspace { CenterContentElement = communication };
        workspace.AddPanel("connection", "连接", connection, PanelZone.Left);
        workspace.AddPanel("log", "日志", log, PanelZone.Bottom);
        workspace.AddPanel("plot", "波形", plot, PanelZone.Bottom);
        workspace.ResetLayout();
        var saved = workspace.SaveLayout();
        workspace.RestoreLayout(saved).Should().BeTrue();
        var contents = workspace.Manager.Layout.Descendents().OfType<LayoutContent>().Select(p => p.Content).ToArray();
        contents.Should().HaveCount(4);
        foreach (var expected in new object[] { communication, connection, log, plot })
            contents.Should().Contain(actual => ReferenceEquals(actual, expected));
    });

    [Fact]
    public void AllTenPages_ShouldKeepInstancesWhenGroupedHiddenAndRestored() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "FlexComDockPages", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection().AddAppServices(directory).BuildServiceProvider();
        try
        {
            var views = new System.Windows.UIElement[]
            {
                new FlexComDotnet.Features.Network.Views.ConnectionConfigView { DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.Network.ViewModels.ConnectionConfigViewModel>() },
                new FlexComDotnet.Features.Serial.Views.CommandListView { DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.Serial.ViewModels.CommandListViewModel>() },
                new FlexComDotnet.Features.AutoReply.Views.AutoReplyView(services.GetRequiredService<FlexComDotnet.Core.Features.AutoReply.ViewModels.AutoReplyViewModel>()),
                new FlexComDotnet.Features.Scripting.Views.ScriptingView(services.GetRequiredService<FlexComDotnet.Core.Features.Scripting.ViewModels.ScriptingViewModel>()),
                new FlexComDotnet.Features.Driver.Views.SerialMonitorView(services.GetRequiredService<FlexComDotnet.Core.Features.Driver.ViewModels.SerialMonitorViewModel>()),
                new FlexComDotnet.Features.ProcessMonitor.Views.ProcessMonitorView { DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.ProcessMonitor.ViewModels.ProcessMonitorViewModel>() },
                new FlexComDotnet.Features.UsbCapture.Views.UsbCaptureView { DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.UsbCapture.ViewModels.UsbCaptureViewModel>() },
                new FlexComDotnet.Features.Visualization.Views.DataVisualizationView(services.GetRequiredService<FlexComDotnet.Core.Features.Visualization.ViewModels.DataVisualizationViewModel>()),
                new FlexComDotnet.Features.Logging.Views.LogPanelView(new(services.GetRequiredService<FlexComDotnet.Core.Features.Logging.Services.ILoggingService>()))
            };
            var main = new FlexComDotnet.Features.Serial.Views.SerialCommunicationView { DataContext = services.GetRequiredService<FlexComDotnet.Core.Features.Serial.ViewModels.SerialCommunicationViewModel>() };
            var workspace = new DockWorkspace { CenterContentElement = main };
            for (var i = 0; i < views.Length; i++) workspace.AddPanel(i.ToString(), i.ToString(), views[i], PanelZone.Right, order: i);
            workspace.ResetLayout();
            foreach (var page in workspace.Manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray()) page.DockAsDocument();
            var layout = workspace.SaveLayout();
            for (var i = 0; i < views.Length; i++) { workspace.HidePanel(i.ToString()); workspace.ShowPanel(i.ToString()); }
            workspace.ResetLayout();
            workspace.RestoreLayout(layout).Should().BeTrue();
            var restored = workspace.Manager.Layout.Descendents().OfType<LayoutContent>().ToArray();
            restored.Should().HaveCount(10);
            for (var i = 0; i < views.Length; i++) restored.Single(p => p.ContentId == i.ToString()).Content.Should().BeSameAs(views[i]);
            restored.Single(p => p.ContentId == "communication").Content.Should().BeSameAs(main);
        }
        finally
        {
            services.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(directory, true);
        }
    });

    [Fact]
    public void RestoreLayout_ShouldPreserveGroupsHiddenStateAndPageInstances() => RunSta(() =>
    {
        var workspace = new DockWorkspace();
        var input = new TextBox { Text = "draft" };
        workspace.CenterContentElement = input;
        workspace.AddPanel("log", "日志", new TextBox(), PanelZone.Bottom);
        workspace.AddPanel("commands", "指令", new TextBox(), PanelZone.Right);
        workspace.ResetLayout();
        var log = workspace.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == "log");
        log.DockAsDocument();
        workspace.HidePanel("commands");
        var saved = workspace.SaveLayout();
        workspace.ResetLayout();

        workspace.RestoreLayout(saved).Should().BeTrue();

        workspace.Manager.Layout.Descendents().OfType<LayoutDocument>().Single().Content.Should().BeSameAs(input);
        workspace.Manager.Layout.Hidden.Single().ContentId.Should().Be("commands");
        workspace.Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Single(p => p.ContentId == "log").Parent.Should().BeOfType<LayoutDocumentPane>();
        input.Text.Should().Be("draft");
    });

    [Fact]
    public void HideAndShow_ShouldKeepContentAndActivateExistingPanel() => RunSta(() =>
    {
        var workspace = new DockWorkspace();
        var input = new TextBox { Text = "未发送的内容" };
        workspace.AddPanel("commands", "指令", input, PanelZone.Right);
        workspace.ResetLayout();

        workspace.HidePanel("commands");
        workspace.ShowPanel("commands");
        workspace.ShowPanel("commands");

        var panel = workspace.Manager.Layout.Descendents().OfType<LayoutAnchorable>().Single();
        panel.Content.Should().BeSameAs(input);
        panel.IsActive.Should().BeTrue();
        input.Text.Should().Be("未发送的内容");
    });

    private static readonly Lazy<System.Windows.Threading.Dispatcher> s_dispatcher = new(() =>
    {
        var ready = new ManualResetEventSlim();
        System.Windows.Threading.Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            var app = new FlexComDotnet.App();
            app.InitializeComponent();
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("WPF initialization timed out.");
        return dispatcher!;
    });

    internal static void RunSta(Action action) => s_dispatcher.Value.Invoke(action);
}
