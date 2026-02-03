namespace FlexComDotnet.Core.Features.Update.Models;

/// <summary>
/// 版本信息
/// </summary>
public record VersionInfo
{
    /// <summary>
    /// 主版本号
    /// </summary>
    public int Major { get; init; }

    /// <summary>
    /// 次版本号
    /// </summary>
    public int Minor { get; init; }

    /// <summary>
    /// 修订版本号
    /// </summary>
    public int Patch { get; init; }

    /// <summary>
    /// 预发布标签 (如 alpha, beta, rc)
    /// </summary>
    public string? Prerelease { get; init; }

    /// <summary>
    /// 原始版本字符串
    /// </summary>
    public string RawVersion { get; init; } = string.Empty;

    /// <summary>
    /// 创建一个空版本
    /// </summary>
    public static VersionInfo Empty => new() { RawVersion = "0.0.0" };

    /// <summary>
    /// 从字符串解析版本号
    /// </summary>
    /// <param name="version">版本字符串，支持 v 前缀，如 "1.0.0" 或 "v1.0.0-beta"</param>
    /// <returns>解析后的版本信息</returns>
    public static VersionInfo Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return Empty;
        }

        var rawVersion = version.Trim();
        var versionToParse = rawVersion;

        // 移除 v 或 V 前缀
        if (versionToParse.StartsWith('v') || versionToParse.StartsWith('V'))
        {
            versionToParse = versionToParse[1..];
        }

        // 分离预发布标签
        string? prerelease = null;
        var prereleaseIndex = versionToParse.IndexOf('-');
        if (prereleaseIndex > 0)
        {
            prerelease = versionToParse[(prereleaseIndex + 1)..];
            versionToParse = versionToParse[..prereleaseIndex];
        }

        // 解析主版本号.次版本号.修订版本号
        var parts = versionToParse.Split('.');
        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;

        return new VersionInfo
        {
            Major = major,
            Minor = minor,
            Patch = patch,
            Prerelease = prerelease,
            RawVersion = rawVersion
        };
    }

    /// <summary>
    /// 尝试解析版本号
    /// </summary>
    public static bool TryParse(string version, out VersionInfo result)
    {
        try
        {
            result = Parse(version);
            return result.Major > 0 || result.Minor > 0 || result.Patch > 0;
        }
        catch
        {
            result = Empty;
            return false;
        }
    }

    /// <summary>
    /// 比较两个版本，返回正数表示当前版本更高，负数表示更低，0表示相等
    /// </summary>
    public int CompareTo(VersionInfo other)
    {
        // 比较主版本号
        if (Major != other.Major)
            return Major.CompareTo(other.Major);

        // 比较次版本号
        if (Minor != other.Minor)
            return Minor.CompareTo(other.Minor);

        // 比较修订版本号
        if (Patch != other.Patch)
            return Patch.CompareTo(other.Patch);

        // 处理预发布版本比较
        // 无预发布标签的版本 > 有预发布标签的版本
        if (string.IsNullOrEmpty(Prerelease) && !string.IsNullOrEmpty(other.Prerelease))
            return 1;
        if (!string.IsNullOrEmpty(Prerelease) && string.IsNullOrEmpty(other.Prerelease))
            return -1;

        // 如果都有预发布标签，按字母顺序比较
        return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断是否比另一个版本更新
    /// </summary>
    public bool IsNewerThan(VersionInfo other) => CompareTo(other) > 0;

    /// <summary>
    /// 获取版本显示字符串
    /// </summary>
    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (!string.IsNullOrEmpty(Prerelease))
        {
            version += $"-{Prerelease}";
        }
        return version;
    }
}
