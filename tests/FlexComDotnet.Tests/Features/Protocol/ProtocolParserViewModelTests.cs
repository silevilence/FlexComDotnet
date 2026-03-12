using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Protocol.ViewModels;
using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Protocol;

public class ProtocolParserViewModelTests
{
    private readonly IChecksumService _checksumService = new ChecksumService();
    private readonly Mock<IConfigurationService> _mockConfigService;

    public ProtocolParserViewModelTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockConfigService.Setup(c => c.Load()).Returns(new AppConfig());
    }

    private ProtocolParserViewModel CreateViewModel(
        IProtocolParserService? parserService = null,
        IScriptManager? scriptManager = null)
    {
        parserService ??= new ProtocolParserService(_checksumService);
        return new ProtocolParserViewModel(parserService, _mockConfigService.Object, scriptManager);
    }

    #region 依赖检查测试

    [Fact]
    public async Task SaveDefinition_WithScriptReference_ShouldTriggerSaveIntercept()
    {
        var protocolService = new ProtocolParserService(_checksumService);
        var mockScriptManager = new Mock<IScriptManager>();

        var scriptInfo = new ScriptFileInfo
        {
            Id = "script1",
            Name = "MyScript",
            FilePath = "scripts/myscript.lua"
        };

        mockScriptManager.Setup(m => m.GetAllScripts()).Returns([scriptInfo]);
        mockScriptManager.Setup(m => m.ReadScriptContent("script1"))
            .Returns("local result = FCom.parse('TestProto', data)");

        var vm = CreateViewModel(protocolService, mockScriptManager.Object);

        bool interceptCalled = false;
        string? interceptedProtocolName = null;
        List<string>? interceptedScripts = null;

        vm.SaveInterceptRequested += (protocolName, scripts) =>
        {
            interceptCalled = true;
            interceptedProtocolName = protocolName;
            interceptedScripts = scripts;
            return Task.FromResult(ProtocolSaveAction.Cancel);
        };

        // Start editing
        vm.NewDefinitionCommand.Execute(null);
        vm.EditingDefinition.Name = "TestProto";

        // Try to save
        await vm.SaveDefinitionCommand.ExecuteAsync(null);

        interceptCalled.Should().BeTrue();
        interceptedProtocolName.Should().Be("TestProto");
        interceptedScripts.Should().Contain("MyScript");
    }

    [Fact]
    public async Task SaveDefinition_WithScriptReference_CancelAction_ShouldNotSave()
    {
        var protocolService = new ProtocolParserService(_checksumService);
        var mockScriptManager = new Mock<IScriptManager>();

        var scriptInfo = new ScriptFileInfo
        {
            Id = "script1",
            Name = "MyScript",
            FilePath = "scripts/myscript.lua"
        };

        mockScriptManager.Setup(m => m.GetAllScripts()).Returns([scriptInfo]);
        mockScriptManager.Setup(m => m.ReadScriptContent("script1"))
            .Returns("FCom.parse('TestProto', data)");

        var vm = CreateViewModel(protocolService, mockScriptManager.Object);

        vm.SaveInterceptRequested += (_, _) =>
            Task.FromResult(ProtocolSaveAction.Cancel);

        vm.NewDefinitionCommand.Execute(null);
        vm.EditingDefinition.Name = "TestProto";

        await vm.SaveDefinitionCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("取消");
        vm.IsEditing.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDefinition_WithScriptReference_ForceSave_ShouldSave()
    {
        var protocolService = new ProtocolParserService(_checksumService);
        var mockScriptManager = new Mock<IScriptManager>();

        var scriptInfo = new ScriptFileInfo
        {
            Id = "script1",
            Name = "MyScript",
            FilePath = "scripts/myscript.lua"
        };

        mockScriptManager.Setup(m => m.GetAllScripts()).Returns([scriptInfo]);
        mockScriptManager.Setup(m => m.ReadScriptContent("script1"))
            .Returns("FCom.parse('TestProto', data)");

        var vm = CreateViewModel(protocolService, mockScriptManager.Object);

        vm.SaveInterceptRequested += (_, _) =>
            Task.FromResult(ProtocolSaveAction.ForceSave);

        vm.NewDefinitionCommand.Execute(null);
        vm.EditingDefinition.Name = "TestProto";

        await vm.SaveDefinitionCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("已保存");
        vm.IsEditing.Should().BeFalse();
    }

    [Fact]
    public async Task SaveDefinition_WithNoScriptReferences_ShouldSaveDirectly()
    {
        var protocolService = new ProtocolParserService(_checksumService);
        var mockScriptManager = new Mock<IScriptManager>();

        mockScriptManager.Setup(m => m.GetAllScripts()).Returns([]);

        var vm = CreateViewModel(protocolService, mockScriptManager.Object);

        bool interceptCalled = false;
        vm.SaveInterceptRequested += (_, _) =>
        {
            interceptCalled = true;
            return Task.FromResult(ProtocolSaveAction.Cancel);
        };

        vm.NewDefinitionCommand.Execute(null);
        vm.EditingDefinition.Name = "SafeProto";

        await vm.SaveDefinitionCommand.ExecuteAsync(null);

        interceptCalled.Should().BeFalse();
        vm.StatusMessage.Should().Contain("已保存");
    }

    [Fact]
    public async Task DeleteDefinition_WithScriptReference_ShouldTriggerDeleteIntercept()
    {
        var protocolService = new ProtocolParserService(_checksumService);
        protocolService.RegisterDefinition(new FrameDefinition { Name = "ToDelete" });

        var mockScriptManager = new Mock<IScriptManager>();
        var scriptInfo = new ScriptFileInfo
        {
            Id = "s1",
            Name = "RefScript",
            FilePath = "scripts/ref.lua"
        };

        mockScriptManager.Setup(m => m.GetAllScripts()).Returns([scriptInfo]);
        mockScriptManager.Setup(m => m.ReadScriptContent("s1"))
            .Returns("FCom.parse('ToDelete', hex)");

        var vm = CreateViewModel(protocolService, mockScriptManager.Object);
        vm.SelectedDefinition = protocolService.GetAllDefinitions().First();

        bool interceptCalled = false;
        vm.DeleteInterceptRequested += (protocolName, scripts) =>
        {
            interceptCalled = true;
            return Task.FromResult(false); // deny deletion
        };

        await vm.DeleteDefinitionCommand.ExecuteAsync(null);

        interceptCalled.Should().BeTrue();
        vm.StatusMessage.Should().Contain("取消");
    }

    [Fact]
    public async Task DeleteDefinition_WithNoScriptReferences_ShouldDeleteDirectly()
    {
        var protocolService = new ProtocolParserService(_checksumService);
        protocolService.RegisterDefinition(new FrameDefinition { Name = "SafeDelete" });

        var mockScriptManager = new Mock<IScriptManager>();
        mockScriptManager.Setup(m => m.GetAllScripts()).Returns([]);

        var vm = CreateViewModel(protocolService, mockScriptManager.Object);
        vm.SelectedDefinition = protocolService.GetAllDefinitions().First();

        bool interceptCalled = false;
        vm.DeleteInterceptRequested += (_, _) =>
        {
            interceptCalled = true;
            return Task.FromResult(false);
        };

        await vm.DeleteDefinitionCommand.ExecuteAsync(null);

        interceptCalled.Should().BeFalse();
        vm.StatusMessage.Should().Contain("已删除");
    }

    #endregion

    #region 基础操作测试

    [Fact]
    public void CreateNew_ShouldEnterEditingMode()
    {
        var vm = CreateViewModel();

        vm.NewDefinitionCommand.Execute(null);

        vm.IsEditing.Should().BeTrue();
        vm.EditingDefinition.Should().NotBeNull();
    }

    [Fact]
    public void CancelEdit_ShouldExitEditingMode()
    {
        var vm = CreateViewModel();

        vm.NewDefinitionCommand.Execute(null);
        vm.IsEditing.Should().BeTrue();

        vm.CancelEditCommand.Execute(null);
        vm.IsEditing.Should().BeFalse();
    }

    #endregion
}
