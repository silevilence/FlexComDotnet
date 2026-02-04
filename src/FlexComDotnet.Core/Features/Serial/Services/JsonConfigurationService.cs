using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 基于 JSON 文件的配置服务实现
/// </summary>
public class JsonConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 保留中文字符，不进行转义
    };

    /// <summary>
    /// 配置文件路径
    /// </summary>
    public string ConfigFilePath { get; }

    /// <summary>
    /// 创建配置服务实例
    /// </summary>
    /// <param name="configFilePath">配置文件路径，默认为应用程序目录下的 config.json</param>
    public JsonConfigurationService(string? configFilePath = null)
    {
        ConfigFilePath = configFilePath ?? GetDefaultConfigPath();
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    /// <returns>应用配置，如果不存在或解析失败则返回默认配置</returns>
    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            
            return config ?? new AppConfig();
        }
        catch (Exception)
        {
            // 配置文件损坏或解析失败，返回默认配置
            return new AppConfig();
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="config">要保存的配置</param>
    /// <returns>保存是否成功</returns>
    public bool Save(AppConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 获取默认配置文件路径
    /// </summary>
    private static string GetDefaultConfigPath()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appDirectory, "config.json");
    }
}
