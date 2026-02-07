using FlexComDotnet.Core.Features.Serial.Models;
using LiteDB;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 使用 LiteDB 实现的指令存储服务
/// </summary>
public class LiteDbCommandStorageService : ICommandStorageService
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<CommandItem> _collection;
    private bool _disposed;

    private const string CollectionName = "commands";

    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// 创建 LiteDB 指令存储服务
    /// </summary>
    /// <param name="databasePath">数据库文件路径，默认为用户数据目录下的 commands.db</param>
    public LiteDbCommandStorageService(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(GetDefaultDataDirectory(), "commands.db");
        _database = new LiteDatabase(DatabasePath);
        _collection = _database.GetCollection<CommandItem>(CollectionName);

        // 确保索引存在
        _collection.EnsureIndex(x => x.SortOrder);
        _collection.EnsureIndex(x => x.IsEnabled);
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandItem> GetAll()
    {
        return _collection.Query()
            .OrderBy(x => x.SortOrder)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public CommandItem? GetById(int id)
    {
        return _collection.FindById(id);
    }

    /// <inheritdoc/>
    public int Add(CommandItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // 自动设置排序顺序为最后
        if (item.SortOrder == 0)
        {
            var maxOrder = _collection.Query()
                .OrderByDescending(x => x.SortOrder)
                .Select(x => x.SortOrder)
                .FirstOrDefault();
            item.SortOrder = maxOrder + 1;
        }

        item.CreatedAt = DateTime.Now;
        item.UpdatedAt = DateTime.Now;

        var result = _collection.Insert(item);
        return result.AsInt32;
    }

    /// <inheritdoc/>
    public bool Update(CommandItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.UpdatedAt = DateTime.Now;
        return _collection.Update(item);
    }

    /// <inheritdoc/>
    public bool Delete(int id)
    {
        return _collection.Delete(id);
    }

    /// <inheritdoc/>
    public bool UpdateSortOrder(IEnumerable<CommandItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        try
        {
            _database.BeginTrans();
            
            foreach (var item in items)
            {
                var existing = _collection.FindById(item.Id);
                if (existing != null)
                {
                    existing.SortOrder = item.SortOrder;
                    existing.UpdatedAt = DateTime.Now;
                    _collection.Update(existing);
                }
            }

            _database.Commit();
            return true;
        }
        catch
        {
            _database.Rollback();
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _database.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// 获取默认数据目录（用户 AppData/Local 目录下的 FlexComDotnet 子目录）
    /// </summary>
    private static string GetDefaultDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDir = Path.Combine(localAppData, "FlexComDotnet");
        
        // 确保目录存在
        if (!Directory.Exists(appDataDir))
        {
            Directory.CreateDirectory(appDataDir);
        }
        
        return appDataDir;
    }
}
