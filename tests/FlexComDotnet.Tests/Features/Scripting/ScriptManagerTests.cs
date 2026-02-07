using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Scripting.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Scripting;

/// <summary>
/// 脚本管理器测试
/// </summary>
public class ScriptManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ScriptManager _manager;

    public ScriptManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "FlexComDotnet_Tests_Scripts_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
        _manager = new ScriptManager(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    #region 初始化测试

    [Fact]
    public void Constructor_ShouldSetScriptsDirectory()
    {
        _manager.ScriptsDirectory.Should().Be(_testDirectory);
    }

    [Fact]
    public void Constructor_ShouldCreateDirectoryIfNotExists()
    {
        var newDir = Path.Combine(Path.GetTempPath(), "FlexCom_Test_New_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var manager = new ScriptManager(newDir);
            Directory.Exists(newDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(newDir))
                Directory.Delete(newDir, recursive: true);
        }
    }

    [Fact]
    public void GetAllScripts_EmptyDirectory_ShouldReturnEmpty()
    {
        var scripts = _manager.GetAllScripts();
        scripts.Should().BeEmpty();
    }

    #endregion

    #region 创建脚本测试

    [Fact]
    public void CreateScript_ShouldCreateFileOnDisk()
    {
        var script = _manager.CreateScript("test_script");

        var filePath = Path.Combine(_testDirectory, script.FilePath);
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void CreateScript_ShouldReturnScriptInfo()
    {
        var script = _manager.CreateScript("my_script");

        script.Should().NotBeNull();
        script.Name.Should().Be("my_script");
        script.Id.Should().NotBeNullOrEmpty();
        script.FilePath.Should().EndWith(".lua");
    }

    [Fact]
    public void CreateScript_WithContent_ShouldWriteContent()
    {
        var script = _manager.CreateScript("content_test", "print('hello')");

        var content = _manager.ReadScriptContent(script.Id);
        content.Should().Be("print('hello')");
    }

    [Fact]
    public void CreateScript_WithoutContent_ShouldUseDefaultTemplate()
    {
        var script = _manager.CreateScript("template_test");

        var content = _manager.ReadScriptContent(script.Id);
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("FCom");
    }

    [Fact]
    public void CreateScript_ShouldAppearInGetAllScripts()
    {
        _manager.CreateScript("listed_script");

        var scripts = _manager.GetAllScripts();
        scripts.Should().HaveCount(1);
        scripts[0].Name.Should().Be("listed_script");
    }

    [Fact]
    public void CreateScript_ShouldRaiseScriptsChangedEvent()
    {
        var eventRaised = false;
        _manager.ScriptsChanged += (_, _) => eventRaised = true;

        _manager.CreateScript("event_test");

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void CreateScript_MultipleScripts_ShouldAllExist()
    {
        _manager.CreateScript("script_1");
        _manager.CreateScript("script_2");
        _manager.CreateScript("script_3");

        var scripts = _manager.GetAllScripts();
        scripts.Should().HaveCount(3);
    }

    #endregion

    #region 读取脚本测试

    [Fact]
    public void GetScript_ExistingId_ShouldReturnScript()
    {
        var created = _manager.CreateScript("find_me");

        var found = _manager.GetScript(created.Id);

        found.Should().NotBeNull();
        found!.Name.Should().Be("find_me");
    }

    [Fact]
    public void GetScript_NonExistingId_ShouldReturnNull()
    {
        var found = _manager.GetScript("non_existing_id");

        found.Should().BeNull();
    }

    [Fact]
    public void ReadScriptContent_ExistingScript_ShouldReturnContent()
    {
        var script = _manager.CreateScript("readable", "local x = 42");

        var content = _manager.ReadScriptContent(script.Id);

        content.Should().Be("local x = 42");
    }

    [Fact]
    public void ReadScriptContent_NonExistingScript_ShouldReturnNull()
    {
        var content = _manager.ReadScriptContent("not_found");

        content.Should().BeNull();
    }

    #endregion

    #region 更新脚本测试

    [Fact]
    public void UpdateScriptInfo_Name_ShouldUpdateName()
    {
        var script = _manager.CreateScript("old_name");

        var result = _manager.UpdateScriptInfo(script.Id, name: "new_name");

        result.Should().BeTrue();
        var updated = _manager.GetScript(script.Id);
        updated!.Name.Should().Be("new_name");
    }

    [Fact]
    public void UpdateScriptInfo_Description_ShouldUpdateDescription()
    {
        var script = _manager.CreateScript("desc_test");

        _manager.UpdateScriptInfo(script.Id, description: "A useful script");

        var updated = _manager.GetScript(script.Id);
        updated!.Description.Should().Be("A useful script");
    }

    [Fact]
    public void UpdateScriptInfo_NonExistingId_ShouldReturnFalse()
    {
        var result = _manager.UpdateScriptInfo("not_found", name: "anything");

        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateScriptInfo_ShouldUpdateLastModifiedAt()
    {
        var script = _manager.CreateScript("time_test");
        var originalTime = script.LastModifiedAt;

        Thread.Sleep(10); // 确保时间差异
        _manager.UpdateScriptInfo(script.Id, name: "updated");

        var updated = _manager.GetScript(script.Id);
        updated!.LastModifiedAt.Should().BeAfter(originalTime);
    }

    [Fact]
    public void SaveScriptContent_ShouldPersistContent()
    {
        var script = _manager.CreateScript("save_test", "original content");

        var result = _manager.SaveScriptContent(script.Id, "updated content");

        result.Should().BeTrue();
        var content = _manager.ReadScriptContent(script.Id);
        content.Should().Be("updated content");
    }

    [Fact]
    public void SaveScriptContent_NonExistingScript_ShouldReturnFalse()
    {
        var result = _manager.SaveScriptContent("not_found", "content");

        result.Should().BeFalse();
    }

    #endregion

    #region 删除脚本测试

    [Fact]
    public void DeleteScript_ExistingScript_ShouldRemove()
    {
        var script = _manager.CreateScript("to_delete");

        var result = _manager.DeleteScript(script.Id);

        result.Should().BeTrue();
        _manager.GetScript(script.Id).Should().BeNull();
        _manager.GetAllScripts().Should().BeEmpty();
    }

    [Fact]
    public void DeleteScript_ShouldDeleteFileFromDisk()
    {
        var script = _manager.CreateScript("disk_delete");
        var filePath = Path.Combine(_testDirectory, script.FilePath);

        _manager.DeleteScript(script.Id);

        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public void DeleteScript_NonExistingId_ShouldReturnFalse()
    {
        var result = _manager.DeleteScript("not_found");

        result.Should().BeFalse();
    }

    [Fact]
    public void DeleteScript_ShouldRaiseScriptsChangedEvent()
    {
        var script = _manager.CreateScript("event_delete");
        var eventRaised = false;
        _manager.ScriptsChanged += (_, _) => eventRaised = true;

        _manager.DeleteScript(script.Id);

        eventRaised.Should().BeTrue();
    }

    #endregion

    #region 名称检查测试

    [Fact]
    public void IsNameExists_ExistingName_ShouldReturnTrue()
    {
        _manager.CreateScript("existing_name");

        _manager.IsNameExists("existing_name").Should().BeTrue();
    }

    [Fact]
    public void IsNameExists_NonExistingName_ShouldReturnFalse()
    {
        _manager.IsNameExists("non_existing").Should().BeFalse();
    }

    [Fact]
    public void IsNameExists_WithExcludeId_ShouldExcludeSelf()
    {
        var script = _manager.CreateScript("self_check");

        _manager.IsNameExists("self_check", excludeId: script.Id).Should().BeFalse();
    }

    #endregion

    #region 默认模板测试

    [Fact]
    public void GetDefaultTemplate_ShouldReturnNonEmpty()
    {
        var template = _manager.GetDefaultTemplate();

        template.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetDefaultTemplate_ShouldContainFComUsage()
    {
        var template = _manager.GetDefaultTemplate();

        template.Should().Contain("FCom");
    }

    #endregion
}
