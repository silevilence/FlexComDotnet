using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Scripting;

/// <summary>
/// 脚本 ViewModel 测试
/// </summary>
public class ScriptingViewModelTests : IDisposable
{
    private readonly Mock<IScriptEngine> _mockEngine;
    private readonly Mock<IScriptManager> _mockManager;
    private readonly Mock<IScriptApiBridge> _mockBridge;
    private readonly ScriptingViewModel _viewModel;

    public ScriptingViewModelTests()
    {
        _mockEngine = new Mock<IScriptEngine>();
        _mockManager = new Mock<IScriptManager>();
        _mockBridge = new Mock<IScriptApiBridge>();

        _mockEngine.Setup(e => e.State).Returns(ScriptState.Idle);
        _mockManager.Setup(m => m.GetAllScripts()).Returns([]);
        _mockManager.Setup(m => m.GetDefaultTemplate()).Returns("-- default template");

        _viewModel = new ScriptingViewModel(
            _mockEngine.Object,
            _mockManager.Object,
            _mockBridge.Object);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    #region 初始化测试

    [Fact]
    public void Constructor_ShouldLoadScriptList()
    {
        _mockManager.Verify(m => m.GetAllScripts(), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithIdleState()
    {
        _viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldHaveEmptyEditor()
    {
        _viewModel.EditorContent.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ShouldHaveEmptyLogs()
    {
        _viewModel.LogEntries.Should().BeEmpty();
    }

    #endregion

    #region 脚本列表管理测试

    [Fact]
    public void CreateNewScript_ShouldCallManager()
    {
        _mockManager.Setup(m => m.CreateScript(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new ScriptFileInfo { Id = "1", Name = "new_script", FilePath = "new_script.lua" });

        _viewModel.NewScriptName = "new_script";
        _viewModel.CreateNewScriptCommand.Execute(null);

        _mockManager.Verify(m => m.CreateScript("new_script", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void CreateNewScript_EmptyName_ShouldNotCreate()
    {
        _viewModel.NewScriptName = "";
        _viewModel.CreateNewScriptCommand.Execute(null);

        _mockManager.Verify(m => m.CreateScript(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void DeleteScript_ShouldCallManager()
    {
        var scriptInfo = new ScriptFileInfo { Id = "1", Name = "to_delete" };
        _mockManager.Setup(m => m.DeleteScript("1")).Returns(true);

        _viewModel.SelectedScript = scriptInfo;
        _viewModel.DeleteScriptCommand.Execute(null);

        _mockManager.Verify(m => m.DeleteScript("1"), Times.Once);
    }

    [Fact]
    public void SelectScript_ShouldLoadContent()
    {
        var scriptInfo = new ScriptFileInfo { Id = "1", Name = "script1" };
        _mockManager.Setup(m => m.ReadScriptContent("1")).Returns("print('hello')");

        _viewModel.SelectedScript = scriptInfo;

        _viewModel.EditorContent.Should().Be("print('hello')");
    }

    #endregion

    #region 脚本执行测试

    [Fact]
    public async Task RunScript_ShouldCallEngine()
    {
        _mockEngine.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScriptExecutionResult.Succeeded(100));

        var scriptInfo = new ScriptFileInfo { Id = "1", Name = "run_test" };
        _mockManager.Setup(m => m.ReadScriptContent("1")).Returns("x = 1");
        _viewModel.SelectedScript = scriptInfo;

        _viewModel.RunScriptCommand.Execute(null);

        // 让异步操作完成
        await Task.Delay(100);

        _mockEngine.Verify(e => e.ExecuteAsync(
            "x = 1",
            "run_test",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void StopScript_ShouldCallEngine()
    {
        _viewModel.StopScriptCommand.Execute(null);

        _mockEngine.Verify(e => e.Stop(), Times.Once);
    }

    #endregion

    #region 保存脚本测试

    [Fact]
    public void SaveScript_ShouldCallManager()
    {
        var scriptInfo = new ScriptFileInfo { Id = "1", Name = "save_test" };
        _mockManager.Setup(m => m.ReadScriptContent("1")).Returns("original");
        _mockManager.Setup(m => m.SaveScriptContent("1", It.IsAny<string>())).Returns(true);

        _viewModel.SelectedScript = scriptInfo;
        _viewModel.EditorContent = "updated content";
        _viewModel.SaveScriptCommand.Execute(null);

        _mockManager.Verify(m => m.SaveScriptContent("1", "updated content"), Times.Once);
    }

    [Fact]
    public void SaveScript_NoSelection_ShouldNotCall()
    {
        _viewModel.SelectedScript = null;
        _viewModel.SaveScriptCommand.Execute(null);

        _mockManager.Verify(m => m.SaveScriptContent(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region 日志测试

    [Fact]
    public void LogOutput_ShouldAddToLogEntries()
    {
        var logEntry = new ScriptLogEntry
        {
            Message = "test log",
            Level = ScriptLogLevel.Info,
            ScriptName = "test"
        };

        _mockEngine.Raise(e => e.LogOutput += null, this, logEntry);

        _viewModel.LogEntries.Should().HaveCount(1);
        _viewModel.LogEntries[0].Message.Should().Be("test log");
    }

    [Fact]
    public void ClearLogs_ShouldEmptyLogEntries()
    {
        var logEntry = new ScriptLogEntry { Message = "to_clear" };
        _mockEngine.Raise(e => e.LogOutput += null, this, logEntry);

        _viewModel.ClearLogsCommand.Execute(null);

        _viewModel.LogEntries.Should().BeEmpty();
    }

    #endregion

    #region 状态同步测试

    [Fact]
    public void StateChanged_Running_ShouldUpdateIsRunning()
    {
        _mockEngine.Raise(e => e.StateChanged += null, this, ScriptState.Running);

        _viewModel.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void StateChanged_Idle_ShouldUpdateIsRunning()
    {
        _mockEngine.Raise(e => e.StateChanged += null, this, ScriptState.Running);
        _mockEngine.Raise(e => e.StateChanged += null, this, ScriptState.Idle);

        _viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void StateChanged_ShouldUpdateStatusText()
    {
        _mockEngine.Raise(e => e.StateChanged += null, this, ScriptState.Running);

        _viewModel.StatusText.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ErrorOccurred 测试

    [Fact]
    public void ErrorOccurred_ShouldAddErrorLog()
    {
        _mockEngine.Raise(e => e.ErrorOccurred += null, this, "script error");

        _viewModel.LogEntries.Should().Contain(e => e.Level == ScriptLogLevel.Error);
    }

    #endregion
}
