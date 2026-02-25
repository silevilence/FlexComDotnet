using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Scripting;

/// <summary>
/// 脚本 Hook 服务测试
/// </summary>
public class ScriptHookServiceTests : IDisposable
{
    private readonly Mock<IScriptManager> _mockScriptManager;
    private readonly Mock<IScriptApiBridge> _mockApiBridge;
    private readonly Mock<IScriptEngine> _mockScriptEngine;
    private readonly Mock<ISerialPortService> _mockSerialPortService;
    private readonly ScriptHookService _service;
    private readonly string _testScriptsDir;

    public ScriptHookServiceTests()
    {
        _mockScriptManager = new Mock<IScriptManager>();
        _mockApiBridge = new Mock<IScriptApiBridge>();
        _mockScriptEngine = new Mock<IScriptEngine>();
        _mockSerialPortService = new Mock<ISerialPortService>();

        _service = new ScriptHookService(
            _mockScriptManager.Object,
            _mockApiBridge.Object,
            _mockScriptEngine.Object,
            _mockSerialPortService.Object);

        _testScriptsDir = Path.Combine(Path.GetTempPath(), $"hook_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testScriptsDir);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_testScriptsDir))
        {
            Directory.Delete(_testScriptsDir, true);
        }
        GC.SuppressFinalize(this);
    }

    #region 初始状态测试

    [Fact]
    public void Constructor_ShouldInitialize_WithDefaultSettings()
    {
        _service.Settings.Should().NotBeNull();
        _service.Settings.RxPreProcessor.Should().NotBeNull();
        _service.Settings.TxPostProcessor.Should().NotBeNull();
        _service.Settings.Reply.Should().NotBeNull();
    }

    #endregion

    #region UpdateSettings 测试

    [Fact]
    public void UpdateSettings_ShouldUpdateSettings()
    {
        var newSettings = new ScriptHookSettings
        {
            RxPreProcessor = new HookConfig { Type = HookType.RxPreProcessor, IsEnabled = true, ScriptId = "script1" }
        };

        _service.UpdateSettings(newSettings);

        _service.Settings.RxPreProcessor.IsEnabled.Should().BeTrue();
        _service.Settings.RxPreProcessor.ScriptId.Should().Be("script1");
    }

    #endregion

    #region SetHookScript 测试

    [Fact]
    public void SetHookScript_ShouldSetScriptId()
    {
        _service.SetHookScript(HookType.RxPreProcessor, "test_script");

        _service.Settings.RxPreProcessor.ScriptId.Should().Be("test_script");
    }

    [Fact]
    public void SetHookScript_WithNull_ShouldClearScriptId()
    {
        _service.SetHookScript(HookType.RxPreProcessor, "test_script");
        _service.SetHookScript(HookType.RxPreProcessor, null);

        _service.Settings.RxPreProcessor.ScriptId.Should().BeNull();
    }

    #endregion

    #region SetHookEnabled 测试

    [Fact]
    public void SetHookEnabled_ShouldEnableHook()
    {
        _service.SetHookEnabled(HookType.TxPostProcessor, true);

        _service.Settings.TxPostProcessor.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SetHookEnabled_ShouldDisableHook()
    {
        _service.SetHookEnabled(HookType.TxPostProcessor, true);
        _service.SetHookEnabled(HookType.TxPostProcessor, false);

        _service.Settings.TxPostProcessor.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region ExecuteRxPreProcessorAsync 测试

    [Fact]
    public async Task ExecuteRxPreProcessorAsync_WhenDisabled_ShouldReturnOriginalData()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = await _service.ExecuteRxPreProcessorAsync(data);

        result.Success.Should().BeTrue();
        result.ProcessedData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ExecuteRxPreProcessorAsync_WhenNoScript_ShouldReturnOriginalData()
    {
        _service.SetHookEnabled(HookType.RxPreProcessor, true);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = await _service.ExecuteRxPreProcessorAsync(data);

        result.Success.Should().BeTrue();
        result.ProcessedData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ExecuteRxPreProcessorAsync_WhenScriptNotFound_ShouldReturnError()
    {
        _service.SetHookEnabled(HookType.RxPreProcessor, true);
        _service.SetHookScript(HookType.RxPreProcessor, "nonexistent");
        _mockScriptManager.Setup(m => m.ReadScriptContent("nonexistent")).Returns((string?)null);

        var data = new byte[] { 0x01, 0x02, 0x03 };
        var result = await _service.ExecuteRxPreProcessorAsync(data);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("脚本不存在");
    }

    #endregion

    #region ExecuteTxPostProcessorAsync 测试

    [Fact]
    public async Task ExecuteTxPostProcessorAsync_WhenDisabled_ShouldReturnOriginalData()
    {
        var data = new byte[] { 0xAA, 0xBB };

        var result = await _service.ExecuteTxPostProcessorAsync(data);

        result.Success.Should().BeTrue();
        result.ProcessedData.Should().BeEquivalentTo(data);
    }

    #endregion

    #region ExecuteReplyHookAsync 测试

    [Fact]
    public async Task ExecuteReplyHookAsync_WhenDisabled_ShouldReturnSkipped()
    {
        var data = new byte[] { 0x01, 0x02 };

        var result = await _service.ExecuteReplyHookAsync(data);

        result.Success.Should().BeTrue();
        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteReplyHookAsync_WhenNoScript_ShouldReturnSkipped()
    {
        _service.SetHookEnabled(HookType.Reply, true);
        var data = new byte[] { 0x01, 0x02 };

        var result = await _service.ExecuteReplyHookAsync(data);

        result.Success.Should().BeTrue();
        result.ShouldReply.Should().BeFalse();
    }

    #endregion

    #region ExecuteTaskAsync 测试

    [Fact]
    public async Task ExecuteTaskAsync_WhenScriptNotFound_ShouldReturnError()
    {
        _mockScriptManager.Setup(m => m.ReadScriptContent("nonexistent")).Returns((string?)null);

        var result = await _service.ExecuteTaskAsync("nonexistent");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("脚本不存在");
    }

    [Fact]
    public async Task ExecuteTaskAsync_ShouldDelegateToScriptEngine()
    {
        var scriptContent = "FCom.log('test')";
        _mockScriptManager.Setup(m => m.ReadScriptContent("task_script")).Returns(scriptContent);
        _mockScriptManager.Setup(m => m.GetScript("task_script")).Returns(new ScriptFileInfo
        {
            Id = "task_script",
            Name = "Task Script",
            FilePath = "/scripts/task.lua"
        });
        _mockScriptEngine.Setup(m => m.ExecuteAsync(scriptContent, "Task Script", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScriptExecutionResult.Succeeded(100));

        var result = await _service.ExecuteTaskAsync("task_script");

        result.Success.Should().BeTrue();
        _mockScriptEngine.Verify(m => m.ExecuteAsync(scriptContent, "Task Script", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
