using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Scripting;

/// <summary>
/// 脚本引擎测试
/// </summary>
public class ScriptEngineTests : IDisposable
{
    private readonly ScriptEngine _engine;
    private readonly Mock<IScriptApiBridge> _mockBridge;

    public ScriptEngineTests()
    {
        _mockBridge = new Mock<IScriptApiBridge>();
        _engine = new ScriptEngine();
        _engine.RegisterApiBridge(_mockBridge.Object);
    }

    public void Dispose()
    {
        _engine.Dispose();
        GC.SuppressFinalize(this);
    }

    #region 初始状态测试

    [Fact]
    public void Constructor_ShouldInitialize_WithIdleState()
    {
        using var engine = new ScriptEngine();
        engine.State.Should().Be(ScriptState.Idle);
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithNullScriptName()
    {
        using var engine = new ScriptEngine();
        engine.CurrentScriptName.Should().BeNull();
    }

    #endregion

    #region 执行脚本测试

    [Fact]
    public async Task ExecuteAsync_SimpleScript_ShouldSucceed()
    {
        var result = await _engine.ExecuteAsync("x = 1 + 1", "test_script");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetCurrentScriptName()
    {
        await _engine.ExecuteAsync("x = 1", "my_script");

        _engine.CurrentScriptName.Should().Be("my_script");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidScript_ShouldReturnFailure()
    {
        var result = await _engine.ExecuteAsync("this is not valid lua !!!", "bad_script");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnElapsedTime()
    {
        var result = await _engine.ExecuteAsync("x = 1", "test_script");

        result.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyScript_ShouldSucceed()
    {
        var result = await _engine.ExecuteAsync("", "empty_script");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RuntimeError_ShouldReturnFailure()
    {
        // 调用一个不存在的函数
        var result = await _engine.ExecuteAsync("nonExistentFunction()", "error_script");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region 状态管理测试

    [Fact]
    public async Task ExecuteAsync_ShouldTransitionToRunningState()
    {
        var stateChanges = new List<ScriptState>();
        _engine.StateChanged += (_, state) => stateChanges.Add(state);

        await _engine.ExecuteAsync("x = 1", "test");

        stateChanges.Should().Contain(ScriptState.Running);
    }

    [Fact]
    public async Task ExecuteAsync_AfterCompletion_ShouldReturnToIdle()
    {
        await _engine.ExecuteAsync("x = 1", "test");

        _engine.State.Should().Be(ScriptState.Idle);
    }

    [Fact]
    public async Task ExecuteAsync_OnError_ShouldSetErrorState()
    {
        var stateChanges = new List<ScriptState>();
        _engine.StateChanged += (_, state) => stateChanges.Add(state);

        await _engine.ExecuteAsync("error('test error')", "error_test");

        stateChanges.Should().Contain(ScriptState.Error);
    }

    #endregion

    #region 停止脚本测试

    [Fact]
    public async Task Stop_WhileRunning_ShouldCancelExecution()
    {
        var resultTask = _engine.ExecuteAsync(
            "while true do end",
            "infinite_loop");

        // 给一些时间让脚本开始执行
        await Task.Delay(100);

        _engine.Stop();

        // 设置超时防止测试无限挂起
        var completedTask = await Task.WhenAny(resultTask, Task.Delay(5000));
        completedTask.Should().Be(resultTask, "脚本应在 Stop 调用后及时终止");

        var result = await resultTask;
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Stop_WhenIdle_ShouldNotThrow()
    {
        var action = () => _engine.Stop();
        action.Should().NotThrow();
    }

    #endregion

    #region API 桥接注册测试

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeLogFunction()
    {
        await _engine.ExecuteAsync("FCom.log('hello')", "bridge_test");

        _mockBridge.Verify(b => b.Log("hello"), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeSendFunction()
    {
        _mockBridge.Setup(b => b.Send(It.IsAny<string>())).Returns(true);

        await _engine.ExecuteAsync("FCom.send('FF 01 02')", "send_test");

        _mockBridge.Verify(b => b.Send("FF 01 02"), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeDelayFunction()
    {
        await _engine.ExecuteAsync("FCom.delay(10)", "delay_test");

        _mockBridge.Verify(b => b.Delay(10), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeCrc16Function()
    {
        _mockBridge.Setup(b => b.Crc16(It.IsAny<string>())).Returns("ABCD");

        await _engine.ExecuteAsync("result = FCom.crc16('01 03')", "crc16_test");

        _mockBridge.Verify(b => b.Crc16("01 03"), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeCrc32Function()
    {
        _mockBridge.Setup(b => b.Crc32(It.IsAny<string>())).Returns("12345678");

        await _engine.ExecuteAsync("result = FCom.crc32('01 03')", "crc32_test");

        _mockBridge.Verify(b => b.Crc32("01 03"), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeChecksumFunction()
    {
        _mockBridge.Setup(b => b.Checksum(It.IsAny<string>())).Returns("04");

        await _engine.ExecuteAsync("result = FCom.checksum('01 03')", "checksum_test");

        _mockBridge.Verify(b => b.Checksum("01 03"), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeGetTimestampFunction()
    {
        _mockBridge.Setup(b => b.GetTimestamp()).Returns(1234567890L);

        await _engine.ExecuteAsync("ts = FCom.getTimestamp()", "timestamp_test");

        _mockBridge.Verify(b => b.GetTimestamp(), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeSendTextFunction()
    {
        _mockBridge.Setup(b => b.SendText(It.IsAny<string>())).Returns(true);

        await _engine.ExecuteAsync("FCom.sendText('hello')", "sendtext_test");

        _mockBridge.Verify(b => b.SendText("hello"), Times.Once);
    }

    [Fact]
    public async Task RegisterApiBridge_ShouldExposeLogLevelFunctions()
    {
        await _engine.ExecuteAsync(@"
            FCom.logDebug('debug msg')
            FCom.logWarning('warn msg')
            FCom.logError('error msg')
        ", "log_levels_test");

        _mockBridge.Verify(b => b.LogDebug("debug msg"), Times.Once);
        _mockBridge.Verify(b => b.LogWarning("warn msg"), Times.Once);
        _mockBridge.Verify(b => b.LogError("error msg"), Times.Once);
    }

    #endregion

    #region 事件测试

    [Fact]
    public async Task ErrorOccurred_ShouldFireOnScriptError()
    {
        string? errorMessage = null;
        _engine.ErrorOccurred += (_, msg) => errorMessage = msg;

        await _engine.ExecuteAsync("error('custom error')", "error_event_test");

        errorMessage.Should().NotBeNull();
        errorMessage.Should().Contain("custom error");
    }

    [Fact]
    public async Task StateChanged_ShouldFireOnStateTransitions()
    {
        var stateChanges = new List<ScriptState>();
        _engine.StateChanged += (_, state) => stateChanges.Add(state);

        await _engine.ExecuteAsync("x = 1", "state_test");

        stateChanges.Should().HaveCountGreaterThanOrEqualTo(2);
        stateChanges.First().Should().Be(ScriptState.Running);
        stateChanges.Last().Should().Be(ScriptState.Idle);
    }

    #endregion

    #region 并发控制测试

    [Fact]
    public async Task ExecuteAsync_WhileRunning_ShouldRejectSecondScript()
    {
        var firstTask = _engine.ExecuteAsync(
            "for i=1,1000000 do x=i end",
            "first_script");

        // 尝试并发执行第二个脚本
        var secondResult = await _engine.ExecuteAsync("y = 2", "second_script");

        await firstTask;

        // 第二个脚本应被拒绝或等待
        // 根据实现可能返回失败或排队
        secondResult.Success.Should().BeFalse();
    }

    #endregion

    #region Dispose 测试

    [Fact]
    public void Dispose_ShouldSetIdleState()
    {
        var engine = new ScriptEngine();
        engine.Dispose();

        engine.State.Should().Be(ScriptState.Idle);
    }

    [Fact]
    public void Dispose_MultipleCalls_ShouldNotThrow()
    {
        var engine = new ScriptEngine();
        var action = () =>
        {
            engine.Dispose();
            engine.Dispose();
        };

        action.Should().NotThrow();
    }

    #endregion
}
