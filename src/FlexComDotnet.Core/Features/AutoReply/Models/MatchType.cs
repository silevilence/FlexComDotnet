namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 匹配类型枚举
/// </summary>
public enum MatchType
{
    /// <summary>
    /// 十六进制包含匹配
    /// </summary>
    HexContains = 0,

    /// <summary>
    /// ASCII 文本包含匹配
    /// </summary>
    AsciiContains = 1,

    /// <summary>
    /// 十六进制完全匹配
    /// </summary>
    HexExact = 2,

    /// <summary>
    /// ASCII 文本完全匹配
    /// </summary>
    AsciiExact = 3,

    /// <summary>
    /// 基于协议结构的触发条件（解析成功/失败作为判定）
    /// </summary>
    ProtocolParse = 4
}

/// <summary>
/// 响应载荷构建模式
/// </summary>
public enum ResponseBuildMode
{
    /// <summary>
    /// 纯文本手动输入
    /// </summary>
    PlainText = 0,

    /// <summary>
    /// 协议动态组帧
    /// </summary>
    ProtocolBuild = 1
}

/// <summary>
/// 字段断言比较运算符
/// </summary>
public enum AssertionOperator
{
    /// <summary>
    /// 等于
    /// </summary>
    Equal = 0,

    /// <summary>
    /// 大于
    /// </summary>
    GreaterThan = 1,

    /// <summary>
    /// 大于等于
    /// </summary>
    GreaterThanOrEqual = 2,

    /// <summary>
    /// 小于
    /// </summary>
    LessThan = 3,

    /// <summary>
    /// 小于等于
    /// </summary>
    LessThanOrEqual = 4,

    /// <summary>
    /// Hex包含
    /// </summary>
    HexContains = 5
}

/// <summary>
/// 字段断言规则
/// </summary>
public partial class FieldAssertion : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    /// <summary>
    /// 字段名称
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _fieldName = string.Empty;

    /// <summary>
    /// 比较运算符
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private AssertionOperator _operator = AssertionOperator.Equal;

    /// <summary>
    /// 期望值（字符串形式）
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _expectedValue = string.Empty;
}

/// <summary>
/// 协议响应配置（用于匹配/顺序规则的协议组帧响应）
/// </summary>
public class ProtocolResponseConfig
{
    /// <summary>
    /// 响应协议名称
    /// </summary>
    public string ProtocolName { get; set; } = string.Empty;

    /// <summary>
    /// 字段值表达式（支持 {} 插值引用接收帧中解析出的字段值）
    /// </summary>
    public Dictionary<string, string> FieldValues { get; set; } = [];

    /// <summary>
    /// 字段 Hex 模式标记（true 表示对应字段值为 Hex 字符串）
    /// </summary>
    public Dictionary<string, bool> FieldHexModes { get; set; } = [];
}
