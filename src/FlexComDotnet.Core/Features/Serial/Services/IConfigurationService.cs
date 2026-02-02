using FlexComDotnet.Core.Features.Serial.Models;

namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 配置服务接口，用于加载和保存应用配置
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 加载配置
    /// </summary>
    /// <returns>应用配置，如果不存在则返回默认配置</returns>
    AppConfig Load();

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="config">要保存的配置</param>
    /// <returns>保存是否成功</returns>
    bool Save(AppConfig config);

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    string ConfigFilePath { get; }
}
