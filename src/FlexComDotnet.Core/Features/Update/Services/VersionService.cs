using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 版本服务实现
/// </summary>
public class VersionService : IVersionService
{
    private readonly VersionInfo _currentVersion;
    private InstallationType? _cachedInstallationType;

    /// <summary>
    /// 获取当前包的完整名称 (仅 MSIX/AppX 打包应用可用)
    /// </summary>
    /// <param name="packageFullNameLength">输入/输出：包名称长度</param>
    /// <param name="packageFullName">输出：包名称</param>
    /// <returns>错误码，0 表示成功，15700L (APPMODEL_ERROR_NO_PACKAGE) 表示非打包应用</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    // APPMODEL_ERROR_NO_PACKAGE 错误码
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

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
    /// 获取当前安装类型 (MSIX 或 Portable)
    /// </summary>
    public InstallationType GetInstallationType()
    {
        if (_cachedInstallationType.HasValue)
        {
            return _cachedInstallationType.Value;
        }

        _cachedInstallationType = DetectInstallationType();
        return _cachedInstallationType.Value;
    }

    /// <summary>
    /// 检测安装类型
    /// </summary>
    private static InstallationType DetectInstallationType()
    {
        try
        {
            // 使用 GetCurrentPackageFullName API 检测是否为 MSIX 打包应用
            // 如果返回 APPMODEL_ERROR_NO_PACKAGE，则表示非打包应用（便携版）
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);

            if (result == APPMODEL_ERROR_NO_PACKAGE)
            {
                // 非打包应用，为便携版
                return InstallationType.Portable;
            }

            // 如果需要更大的缓冲区或成功，说明是 MSIX 打包应用
            if (result == 0 || length > 0)
            {
                return InstallationType.Msix;
            }

            // 其他情况默认为便携版
            return InstallationType.Portable;
        }
        catch
        {
            return InstallationType.Unknown;
        }
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
