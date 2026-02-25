using FlexComDotnet.Core.Features.AutoReply.Models;
using FlexComDotnet.Core.Features.AutoReply.Services.Handlers;
using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.AutoReply;

/// <summary>
/// 脚本回复处理器测试
/// </summary>
public class ScriptReplyHandlerTests
{
    private readonly Mock<IScriptHookService> _mockHookService;
    private readonly ScriptReplyHandler _handler;

    public ScriptReplyHandlerTests()
    {
        _mockHookService = new Mock<IScriptHookService>();
        _handler = new ScriptReplyHandler(_mockHookService.Object);
    }

    #region 基本属性测试

    [Fact]
    public void Mode_ShouldBeScript()
    {
        _handler.Mode.Should().Be(ReplyMode.Script);
    }

    [Fact]
    public void DisplayName_ShouldBeCorrect()
    {
        _handler.DisplayName.Should().Be("脚本回复");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _handler.Description.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Process 测试

    [Fact]
    public void Process_WithEmptyData_ShouldReturnNoReply()
    {
        var config = new AutoReplyConfig();

        var result = _handler.Process([], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WithNoScriptId_ShouldReturnNoReply()
    {
        var config = new AutoReplyConfig
        {
            ScriptConfig = new ScriptReplyConfig { ScriptId = null }
        };

        var result = _handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WhenHookFails_ShouldReturnNoReply()
    {
        var config = new AutoReplyConfig
        {
            ScriptConfig = new ScriptReplyConfig { ScriptId = "test_script" }
        };
        _mockHookService.Setup(m => m.ExecuteReplyHookAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(HookExecutionResult.Failed("Error"));

        var result = _handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WhenHookReturnsNoReply_ShouldReturnNoReply()
    {
        var config = new AutoReplyConfig
        {
            ScriptConfig = new ScriptReplyConfig { ScriptId = "test_script" }
        };
        _mockHookService.Setup(m => m.ExecuteReplyHookAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(HookExecutionResult.SuccessNoReply());

        var result = _handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    [Fact]
    public void Process_WhenHookReturnsReply_ShouldReturnReplyData()
    {
        var config = new AutoReplyConfig
        {
            ScriptConfig = new ScriptReplyConfig { ScriptId = "test_script" }
        };
        var replyData = new byte[] { 0xAA, 0xBB, 0xCC };
        _mockHookService.Setup(m => m.ExecuteReplyHookAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(HookExecutionResult.SuccessWithReply(replyData));

        var result = _handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeTrue();
        result.ResponseData.Should().BeEquivalentTo(replyData);
        result.MatchedRuleName.Should().Be("脚本回复");
    }

    [Fact]
    public void Process_WhenHookReturnsEmptyReply_ShouldReturnNoReply()
    {
        var config = new AutoReplyConfig
        {
            ScriptConfig = new ScriptReplyConfig { ScriptId = "test_script" }
        };
        _mockHookService.Setup(m => m.ExecuteReplyHookAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(HookExecutionResult.SuccessWithReply([]));

        var result = _handler.Process([0x01, 0x02], config);

        result.ShouldReply.Should().BeFalse();
    }

    #endregion

    #region Reset 测试

    [Fact]
    public void Reset_ShouldNotThrow()
    {
        var config = new AutoReplyConfig();

        var action = () => _handler.Reset(config);

        action.Should().NotThrow();
    }

    #endregion
}
