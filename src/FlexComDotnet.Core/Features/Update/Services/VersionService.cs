using System.Reflection;
using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 版本服务实现
/// </summary>
public class VersionService : IVersionService
{
    private readonly VersionInfo _currentVersion;

    public VersionService()
    {
        _currentVersion = GetVersionFromAssembly();
    }

    /// <summary>
    /// 获取当前应用版本
    /// </summary>
    public VersionInfo GetCurrentVersion() => _currentVersion;

    /// <summary>
    /// 比较两个版本
    /// </summary>
    public int CompareVersions(VersionInfo version1, VersionInfo version2)
    {
        return version1.CompareTo(version2);
    }

    /// <summary>
    /// 判断远程版本是否比本地版本更新
    /// </summary>
    public bool IsUpdateAvailable(VersionInfo remoteVersion)
    {
        return remoteVersion.IsNewerThan(_currentVersion);
    }

    /// <summary>
    /// 从程序集获取版本信息
    /// </summary>
    private static VersionInfo GetVersionFromAssembly()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;

        if (version is null)
        {
            return VersionInfo.Empty;
        }

        return new VersionInfo
        {
            Major = version.Major,
            Minor = version.Minor,
            Patch = version.Build >= 0 ? version.Build : 0,
            RawVersion = $"{version.Major}.{version.Minor}.{(version.Build >= 0 ? version.Build : 0)}"
        };
    }
}
