using FlexComDotnet.Core.Features.Scripting.Models;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Scripting;

/// <summary>
/// Hook 模型测试
/// </summary>
public class HookModelsTests
{
    #region HookType 测试

    [Fact]
    public void HookType_ShouldHaveCorrectValues()
    {
        ((int)HookType.RxPreProcessor).Should().Be(0);
        ((int)HookType.TxPostProcessor).Should().Be(1);
        ((int)HookType.Reply).Should().Be(2);
        ((int)HookType.Task).Should().Be(3);
    }

    #endregion

    #region HookConfig 测试

    [Fact]
    public void HookConfig_DefaultValues_ShouldBeCorrect()
    {
        var config = new HookConfig();

        config.Type.Should().Be(HookType.RxPreProcessor);
        config.ScriptId.Should().BeNull();
        config.IsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(HookType.RxPreProcessor, "接收预处理")]
    [InlineData(HookType.TxPostProcessor, "发送后处理")]
    [InlineData(HookType.Reply, "脚本应答")]
    [InlineData(HookType.Task, "自动化任务")]
    public void HookConfig_DisplayName_ShouldReturnCorrectName(HookType type, string expectedName)
    {
        var config = new HookConfig { Type = type };

        config.DisplayName.Should().Be(expectedName);
    }

    #endregion

    #region ScriptHookSettings 测试

    [Fact]
    public void ScriptHookSettings_DefaultValues_ShouldBeCorrect()
    {
        var settings = new ScriptHookSettings();

        settings.RxPreProcessor.Should().NotBeNull();
        settings.RxPreProcessor.Type.Should().Be(HookType.RxPreProcessor);

        settings.TxPostProcessor.Should().NotBeNull();
        settings.TxPostProcessor.Type.Should().Be(HookType.TxPostProcessor);

        settings.Reply.Should().NotBeNull();
        settings.Reply.Type.Should().Be(HookType.Reply);
    }

    #endregion

    #region HookExecutionResult 测试

    [Fact]
    public void SuccessWithData_ShouldCreateCorrectResult()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = HookExecutionResult.SuccessWithData(data, 100);

        result.Success.Should().BeTrue();
        result.ProcessedData.Should().BeEquivalentTo(data);
        result.ElapsedMs.Should().Be(100);
        result.ShouldReply.Should().BeFalse();
        result.ReplyData.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SuccessWithReply_ShouldCreateCorrectResult()
    {
        var replyData = new byte[] { 0xAA, 0xBB };

        var result = HookExecutionResult.SuccessWithReply(replyData, 50);

        result.Success.Should().BeTrue();
        result.ShouldReply.Should().BeTrue();
        result.ReplyData.Should().BeEquivalentTo(replyData);
        result.ElapsedMs.Should().Be(50);
        result.ProcessedData.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SuccessNoReply_ShouldCreateCorrectResult()
    {
        var result = HookExecutionResult.SuccessNoReply(25);

        result.Success.Should().BeTrue();
        result.ShouldReply.Should().BeFalse();
        result.ElapsedMs.Should().Be(25);
        result.ReplyData.Should().BeNull();
        result.ProcessedData.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failed_ShouldCreateCorrectResult()
    {
        var result = HookExecutionResult.Failed("Test error", 10);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test error");
        result.ElapsedMs.Should().Be(10);
        result.ShouldReply.Should().BeFalse();
        result.ReplyData.Should().BeNull();
        result.ProcessedData.Should().BeNull();
    }

    [Fact]
    public void Skipped_ShouldCreateCorrectResult()
    {
        var result = HookExecutionResult.Skipped();

        result.Success.Should().BeTrue();
        result.ElapsedMs.Should().Be(0);
        result.ShouldReply.Should().BeFalse();
        result.ReplyData.Should().BeNull();
        result.ProcessedData.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    #endregion
}
