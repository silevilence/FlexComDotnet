using FlexComDotnet.Features.Layout.Services;
using FlexComDotnet.Features.Layout.Controls;
using FlexComDotnet.Core.Features.Layout.Models;
using FluentAssertions;
using System.Windows.Controls;

namespace FlexComDotnet.Tests.Features.Layout;

public class DockLayoutStoreTests
{
    [Fact]
    public void IsolatedServices_ShouldWriteConfigurationAndCommandsUnderRequestedDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FlexComDockTests", Guid.NewGuid().ToString("N"));
        try
        {
            var collection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            FlexComDotnet.Services.ServiceCollectionExtensions.AddAppServices(collection, directory);
            using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(collection);
            var config = (FlexComDotnet.Core.Features.Serial.Services.JsonConfigurationService)provider.GetService(typeof(FlexComDotnet.Core.Features.Serial.Services.IConfigurationService))!;
            var commands = (FlexComDotnet.Core.Features.Serial.Services.LiteDbCommandStorageService)provider.GetService(typeof(FlexComDotnet.Core.Features.Serial.Services.ICommandStorageService))!;
            config.Save(new());
            config.ConfigFilePath.Should().Be(Path.Combine(directory, "config.json"));
            commands.DatabasePath.Should().Be(Path.Combine(directory, "commands.db"));
            File.Exists(config.ConfigFilePath).Should().BeTrue();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void LoadAndSave_ShouldBackUpLegacyAndRecoverCorruptLayoutWithoutTouchingConfig() => DockWorkspaceTests.RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "FlexComDockTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configPath = Path.Combine(directory, "config.json");
            File.WriteAllText(configPath, "original business configuration");
            var layoutPath = Path.Combine(directory, "workspace.xml");
            var workspace = new DockWorkspace { CenterContentElement = new TextBox { Text = "input" } };
            workspace.AddPanel("log", "日志", new TextBox(), PanelZone.Bottom);
            var store = new DockLayoutStore(layoutPath);
            store.Load(workspace, new LayoutState(), configPath);
            File.ReadAllText(configPath + ".before-docking").Should().Be("original business configuration");
            workspace.HidePanel("log");
            store.Save(workspace);
            File.ReadAllText(layoutPath).Should().Contain("FlexComVersion=\"1\"").And.NotContain("input");
            File.WriteAllText(layoutPath, "<corrupt");
            store.Load(workspace, new LayoutState(), configPath).Should().BeFalse();
            File.ReadAllText(layoutPath + ".invalid").Should().Be("<corrupt");
            File.ReadAllText(configPath).Should().Be("original business configuration");
            workspace.CenterContentElement.Should().BeOfType<TextBox>().Which.Text.Should().Be("input");
        }
        finally { Directory.Delete(directory, true); }
    });
}
