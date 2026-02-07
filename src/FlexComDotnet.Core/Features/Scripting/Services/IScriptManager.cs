using FlexComDotnet.Core.Features.Scripting.Models;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本管理器接口 - 负责脚本文件的增删改查
/// </summary>
public interface IScriptManager
{
    /// <summary>
    /// 脚本存储目录路径
    /// </summary>
    string ScriptsDirectory { get; }

    /// <summary>
    /// 脚本列表变更事件
    /// </summary>
    event EventHandler? ScriptsChanged;

    /// <summary>
    /// 获取所有脚本文件信息
    /// </summary>
    /// <returns>脚本文件信息列表</returns>
    IReadOnlyList<ScriptFileInfo> GetAllScripts();

    /// <summary>
    /// 根据 ID 获取脚本信息
    /// </summary>
    /// <param name="scriptId">脚本 ID</param>
    /// <returns>脚本文件信息，不存在则返回 null</returns>
    ScriptFileInfo? GetScript(string scriptId);

    /// <summary>
    /// 创建新脚本
    /// </summary>
    /// <param name="name">脚本名称</param>
    /// <param name="content">脚本内容（默认模板）</param>
    /// <returns>创建的脚本文件信息</returns>
    ScriptFileInfo CreateScript(string name, string? content = null);

    /// <summary>
    /// 更新脚本元信息
    /// </summary>
    /// <param name="scriptId">脚本 ID</param>
    /// <param name="name">新的脚本名称</param>
    /// <param name="description">新的脚本描述</param>
    /// <returns>是否更新成功</returns>
    bool UpdateScriptInfo(string scriptId, string? name = null, string? description = null);

    /// <summary>
    /// 读取脚本内容
    /// </summary>
    /// <param name="scriptId">脚本 ID</param>
    /// <returns>脚本代码内容，不存在则返回 null</returns>
    string? ReadScriptContent(string scriptId);

    /// <summary>
    /// 保存脚本内容
    /// </summary>
    /// <param name="scriptId">脚本 ID</param>
    /// <param name="content">脚本代码内容</param>
    /// <returns>是否保存成功</returns>
    bool SaveScriptContent(string scriptId, string content);

    /// <summary>
    /// 删除脚本
    /// </summary>
    /// <param name="scriptId">脚本 ID</param>
    /// <returns>是否删除成功</returns>
    bool DeleteScript(string scriptId);

    /// <summary>
    /// 检查脚本名称是否已存在
    /// </summary>
    /// <param name="name">脚本名称</param>
    /// <param name="excludeId">排除的脚本 ID（用于编辑时排除自身）</param>
    /// <returns>是否已存在同名脚本</returns>
    bool IsNameExists(string name, string? excludeId = null);

    /// <summary>
    /// 获取默认脚本模板
    /// </summary>
    /// <returns>默认 Lua 脚本模板</returns>
    string GetDefaultTemplate();
}
