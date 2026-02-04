using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 版本服务接口
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// 获取当前应用版本
    /// </summary>
    /// <returns>当前版本信息</returns>
    VersionInfo GetCurrentVersion();

    /// <summary>
    /// 比较两个版本
    /// </summary>
    /// <param name="version1">版本1</param>
    /// <param name="version2">版本2</param>
    /// <returns>正数表示版本1更高，负数表示版本2更高，0表示相等</returns>
    int CompareVersions(VersionInfo version1, VersionInfo version2);

    /// <summary>
    /// 判断远程版本是否比本地版本更新
    /// </summary>
    /// <param name="remoteVersion">远程版本</param>
    /// <returns>如果远程版本更新则返回 true</returns>
    bool IsUpdateAvailable(VersionInfo remoteVersion);

    /// <summary>
    /// 获取当前安装类型 (MSIX 或 Portable)
    /// </summary>
    /// <returns>安装类型</returns>
    InstallationType GetInstallationType();
}
