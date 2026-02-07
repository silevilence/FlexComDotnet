using System.Text.Json;
using FlexComDotnet.Core.Features.Scripting.Models;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本管理器实现 - 负责脚本文件的增删改查
/// </summary>
public class ScriptManager : IScriptManager
{
    private readonly string _scriptsDirectory;
    private readonly string _metadataFilePath;
    private readonly List<ScriptFileInfo> _scripts = [];
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public string ScriptsDirectory => _scriptsDirectory;

    /// <inheritdoc />
    public event EventHandler? ScriptsChanged;

    public ScriptManager(string scriptsDirectory)
    {
        _scriptsDirectory = scriptsDirectory;
        _metadataFilePath = Path.Combine(_scriptsDirectory, "_scripts.json");

        // 确保脚本目录存在
        if (!Directory.Exists(_scriptsDirectory))
        {
            Directory.CreateDirectory(_scriptsDirectory);
        }

        LoadMetadata();
    }

    #region 查询操作

    /// <inheritdoc />
    public IReadOnlyList<ScriptFileInfo> GetAllScripts()
    {
        lock (_lock)
        {
            return _scripts.Select(s => s.Clone()).ToList().AsReadOnly();
        }
    }

    /// <inheritdoc />
    public ScriptFileInfo? GetScript(string scriptId)
    {
        lock (_lock)
        {
            return _scripts.FirstOrDefault(s => s.Id == scriptId)?.Clone();
        }
    }

    /// <inheritdoc />
    public string? ReadScriptContent(string scriptId)
    {
        lock (_lock)
        {
            var script = _scripts.FirstOrDefault(s => s.Id == scriptId);
            if (script == null) return null;

            var fullPath = Path.Combine(_scriptsDirectory, script.FilePath);
            if (!File.Exists(fullPath)) return null;

            return File.ReadAllText(fullPath);
        }
    }

    #endregion

    #region 创建操作

    /// <inheritdoc />
    public ScriptFileInfo CreateScript(string name, string? content = null)
    {
        lock (_lock)
        {
            var id = Guid.NewGuid().ToString("N");
            var fileName = $"{SanitizeFileName(name)}_{id[..8]}.lua";
            var now = DateTime.Now;

            var scriptInfo = new ScriptFileInfo
            {
                Id = id,
                Name = name,
                FilePath = fileName,
                CreatedAt = now,
                LastModifiedAt = now
            };

            // 写入脚本文件
            var fullPath = Path.Combine(_scriptsDirectory, fileName);
            var scriptContent = content ?? GetDefaultTemplate();
            File.WriteAllText(fullPath, scriptContent);

            // 添加到列表并保存元数据
            _scripts.Add(scriptInfo);
            SaveMetadata();

            ScriptsChanged?.Invoke(this, EventArgs.Empty);

            return scriptInfo.Clone();
        }
    }

    #endregion

    #region 更新操作

    /// <inheritdoc />
    public bool UpdateScriptInfo(string scriptId, string? name = null, string? description = null)
    {
        lock (_lock)
        {
            var script = _scripts.FirstOrDefault(s => s.Id == scriptId);
            if (script == null) return false;

            if (name != null) script.Name = name;
            if (description != null) script.Description = description;
            script.LastModifiedAt = DateTime.Now;

            SaveMetadata();
            ScriptsChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }

    /// <inheritdoc />
    public bool SaveScriptContent(string scriptId, string content)
    {
        lock (_lock)
        {
            var script = _scripts.FirstOrDefault(s => s.Id == scriptId);
            if (script == null) return false;

            var fullPath = Path.Combine(_scriptsDirectory, script.FilePath);

            try
            {
                File.WriteAllText(fullPath, content);
                script.LastModifiedAt = DateTime.Now;
                SaveMetadata();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region 删除操作

    /// <inheritdoc />
    public bool DeleteScript(string scriptId)
    {
        lock (_lock)
        {
            var script = _scripts.FirstOrDefault(s => s.Id == scriptId);
            if (script == null) return false;

            // 删除磁盘文件
            var fullPath = Path.Combine(_scriptsDirectory, script.FilePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            // 从列表移除
            _scripts.Remove(script);
            SaveMetadata();

            ScriptsChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }

    #endregion

    #region 工具方法

    /// <inheritdoc />
    public bool IsNameExists(string name, string? excludeId = null)
    {
        lock (_lock)
        {
            return _scripts.Any(s =>
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                s.Id != excludeId);
        }
    }

    /// <inheritdoc />
    public string GetDefaultTemplate()
    {
        return """
            -- FlexComDotnet Lua 脚本
            -- 可用 API:
            --   FCom.send(hexString)      发送十六进制数据 (如 "FF 01 02")
            --   FCom.sendText(text)        发送文本数据
            --   FCom.log(message)          输出日志
            --   FCom.logDebug(message)     输出调试日志
            --   FCom.logWarning(message)   输出警告日志
            --   FCom.logError(message)     输出错误日志
            --   FCom.delay(ms)             延时 (毫秒)
            --   FCom.crc16(hexString)      计算 CRC16-Modbus
            --   FCom.crc32(hexString)      计算 CRC32
            --   FCom.checksum(hexString)   计算 Checksum (Sum8)
            --   FCom.getTimestamp()         获取时间戳 (ms)
            --   FCom.hexToBytes(hexString) Hex 转字节数组
            --   FCom.bytesToHex(bytes)     字节数组转 Hex

            FCom.log("脚本开始执行")

            -- 在此编写你的脚本逻辑
            -- 示例: 发送数据并等待
            -- FCom.send("01 03 00 00 00 01")
            -- FCom.delay(100)

            FCom.log("脚本执行完成")
            """;
    }

    #endregion

    #region 私有方法

    private void LoadMetadata()
    {
        if (!File.Exists(_metadataFilePath)) return;

        try
        {
            var json = File.ReadAllText(_metadataFilePath);
            var scripts = JsonSerializer.Deserialize<List<ScriptFileInfo>>(json, JsonOptions);
            if (scripts != null)
            {
                _scripts.Clear();
                _scripts.AddRange(scripts);
            }
        }
        catch
        {
            // 忽略反序列化错误，使用空列表
        }
    }

    private void SaveMetadata()
    {
        try
        {
            var json = JsonSerializer.Serialize(_scripts, JsonOptions);
            File.WriteAllText(_metadataFilePath, json);
        }
        catch
        {
            // 忽略序列化错误
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());

        // 限制长度
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return sanitized;
    }

    #endregion
}
