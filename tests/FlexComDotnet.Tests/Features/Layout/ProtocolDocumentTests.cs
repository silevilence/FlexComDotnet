using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Protocol.ViewModels;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FlexComDotnet.Features.Layout.Controls;
using FlexComDotnet.Features.Protocol.Views;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Layout;

public class ProtocolDocumentTests
{
    [Theory]
    [InlineData(MessageBoxResult.Cancel, true, false)]
    [InlineData(MessageBoxResult.No, true, true)]
    [InlineData(MessageBoxResult.Yes, false, false)]
    [InlineData(MessageBoxResult.Yes, true, true)]
    public void CloseEditor_ShouldRespectDecisionAndWriteFailure(MessageBoxResult decision, bool saveSucceeds, bool canClose) => DockWorkspaceTests.RunSta(() =>
    {
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new AppConfig());
        config.Setup(c => c.Save(It.IsAny<AppConfig>())).Returns(saveSucceeds);
        var vm = new ProtocolParserViewModel(new ProtocolParserService(new ChecksumService()), config.Object);
        vm.NewDefinitionCommand.Execute(null);
        vm.EditingDefinition.Name = "draft";
        vm.MarkDirty();
        var view = new ProtocolDefinitionView(vm, () => decision);
        view.RequestCloseAsync().GetAwaiter().GetResult().Should().Be(canClose);
        if (!canClose) { vm.IsDirty.Should().BeTrue(); vm.EditingDefinition.Name.Should().Be("draft"); }
        config.Verify(c => c.Save(It.IsAny<AppConfig>()), decision == MessageBoxResult.Yes ? Times.Once() : Times.Never());
        view.Release();
    });

    [Fact]
    public void Editor_ShouldKeepActionsReachableInSmallDockPane() => DockWorkspaceTests.RunSta(() =>
    {
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new AppConfig());
        var view = new ProtocolDefinitionView(new ProtocolParserViewModel(new ProtocolParserService(new ChecksumService()), config.Object));
        view.Measure(new Size(320, 200));
        view.Arrange(new Rect(0, 0, 320, 200));
        view.UpdateLayout();
        var viewport = view.Content.Should().BeOfType<ScrollViewer>().Subject;
        viewport.ScrollableHeight.Should().BeGreaterThan(0);
        viewport.ScrollableWidth.Should().BeGreaterThan(0);
        view.Release();
    });

    [Fact]
    public void TxAndRxDocuments_ShouldKeepInputAndBackfillTheSharedSession() => DockWorkspaceTests.RunSta(() =>
    {
        var parser = new ProtocolParserService(new ChecksumService());
        parser.RegisterDefinition(new FrameDefinition
        {
            Name = "DockTest", Header = "AA", Trailer = "55", MinFrameLength = 2, MaxFrameLength = 2
        });
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.Load()).Returns(new AppConfig());
        using var vm = new SerialCommunicationViewModel(Mock.Of<ISerialPortService>(), config.Object, Mock.Of<ILogSaveService>());
        vm.SendText = "A";
        var tx = new TxProtocolBuildView(parser, vm);
        var workspace = new DockWorkspace();
        workspace.ResetLayout();
        workspace.OpenDocument("tx", "组帧", () => tx);
        ((ComboBox)tx.FindName("ProtocolComboBox")).SelectedIndex = 0;
        tx.Measure(new Size(800, 600)); tx.Arrange(new Rect(0, 0, 800, 600)); tx.UpdateLayout();
        FindButtons(tx).Single(b => Equals(b.Content, "组帧")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var result = ((TextBox)tx.FindName("BuildResultTextBox")).Text;
        result.Should().Be("AA 55");
        var rx = new RxProtocolParseView(parser, result);
        workspace.OpenDocument("rx", "解析", () => rx);
        workspace.ResetLayout();
        workspace.RestoreLayout(workspace.SaveLayout()).Should().BeTrue();
        ((TextBox)tx.FindName("BuildResultTextBox")).Text.Should().Be(result);
        ((TextBox)rx.FindName("HexInputTextBox")).Text.Should().Be(result);
        FindButtons(tx).Single(b => Equals(b.Content, "追加回填")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        vm.IsHexSendMode.Should().BeTrue();
        vm.SendText.Should().Be("41 AA 55");
        FindButtons(tx).Single(b => Equals(b.Content, "覆盖回填")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        vm.SendText.Should().Be(result);
        ((ComboBox)rx.FindName("ProtocolComboBox")).SelectedIndex = 0;
        ((TextBlock)rx.FindName("StatusText")).Text.Should().Contain("解析成功");
    });

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        if (root is Button button) yield return button;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in FindButtons(child)) yield return item;
    }
}
