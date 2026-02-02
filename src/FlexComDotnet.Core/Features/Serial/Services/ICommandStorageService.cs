using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 指令存储服务接口
/// </summary>
public interface ICommandStorageService : IDisposable
{
    /// <summary>
    /// 获取所有指令（按排序顺序）
    /// </summary>
    IReadOnlyList<CommandItem> GetAll();

    /// <summary>
    /// 根据 ID 获取指令
    /// </summary>
    CommandItem? GetById(int id);

    /// <summary>
    /// 添加指令
    /// </summary>
    /// <returns>新增指令的 ID</returns>
    int Add(CommandItem item);

    /// <summary>
    /// 更新指令
    /// </summary>
    /// <returns>是否成功</returns>
    bool Update(CommandItem item);

    /// <summary>
    /// 删除指令
    /// </summary>
    /// <returns>是否成功</returns>
    bool Delete(int id);

    /// <summary>
    /// 更新多条指令的排序顺序
    /// </summary>
    /// <param name="items">包含新排序顺序的指令列表</param>
    /// <returns>是否成功</returns>
    bool UpdateSortOrder(IEnumerable<CommandItem> items);

    /// <summary>
    /// 获取数据库文件路径
    /// </summary>
    string DatabasePath { get; }
}
