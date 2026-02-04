namespace FlexComDotnet.Core.Features.Update.Models;

/// <summary>
/// 应用程序安装类型
/// </summary>
public enum InstallationType
{
    /// <summary>
    /// MSIX 安装包 (通过 Windows 应用商店或 MSIX 安装)
    /// </summary>
    Msix,

    /// <summary>
    /// ZIP 便携版 (直接解压运行)
    /// </summary>
    Portable,

    /// <summary>
    /// 未知安装类型
    /// </summary>
    Unknown
}
