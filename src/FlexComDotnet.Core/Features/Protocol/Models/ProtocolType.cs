namespace FlexComDotnet.Core.Features.Protocol.Models;

/// <summary>
/// 协议类型枚举
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// 通用协议 (可自定义帧头、帧尾、校验等)
    /// </summary>
    Generic,

    /// <summary>
    /// DL/T 645-2007 电表协议
    /// </summary>
    Dlt645
}

/// <summary>
/// 协议保存拦截操作 - 用户在依赖冲突时选择的动作
/// </summary>
public enum ProtocolSaveAction
{
    /// <summary>
    /// 强制保存（覆盖）
    /// </summary>
    ForceSave,

    /// <summary>
    /// 另存为新协议（克隆模式）
    /// </summary>
    CloneAsNew,

    /// <summary>
    /// 取消保存
    /// </summary>
    Cancel
}
