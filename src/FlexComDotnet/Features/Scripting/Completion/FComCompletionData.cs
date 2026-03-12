using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace FlexComDotnet.Features.Scripting.Completion;

/// <summary>
/// FCom API 自动补全数据项 - 支持深色主题和动态匹配高亮
/// </summary>
public class FComCompletionData : ICompletionData
{
    private readonly string _description;
    private readonly string _returnType;
    private readonly string? _parameters;
    
    /// <summary>
    /// 动态获取当前输入前缀的委托
    /// </summary>
    public Func<string>? GetCurrentPrefix { get; set; }

    public FComCompletionData(string text, string description, string returnType = "void", string? parameters = null)
    {
        Text = text;
        _description = description;
        _returnType = returnType;
        _parameters = parameters;
    }

    public ImageSource? Image => null;

    public string Text { get; }

    /// <summary>
    /// 显示内容 - 带动态匹配高亮的 TextBlock
    /// </summary>
    public object Content
    {
        get
        {
            var textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = 12
            };

            // 动态获取当前输入的前缀
            var currentPrefix = GetCurrentPrefix?.Invoke() ?? string.Empty;

            // 根据实际匹配前缀动态高亮
            if (!string.IsNullOrEmpty(currentPrefix) && Text.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // 已匹配部分 - 高亮色
                textBlock.Inlines.Add(new Run(Text[..currentPrefix.Length])
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(79, 193, 255)) // 亮蓝色
                });
                // 未匹配部分
                textBlock.Inlines.Add(new Run(Text[currentPrefix.Length..])
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212))
                });
            }
            else
            {
                textBlock.Inlines.Add(new Run(Text)
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212))
                });
            }

            // 添加参数信息（灰色）
            if (_parameters != null)
            {
                textBlock.Inlines.Add(new Run($"({_parameters})")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
                });
            }

            return textBlock;
        }
    }

    /// <summary>
    /// 描述信息 - 深色主题样式的 ToolTip
    /// </summary>
    public object Description
    {
        get
        {
            var paramInfo = _parameters != null ? $"({_parameters})" : "()";
            
            var panel = new StackPanel
            {
                MaxWidth = 350
            };

            // 函数签名
            var signature = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
            signature.Inlines.Add(new Run(Text) { Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 170)) });
            signature.Inlines.Add(new Run(paramInfo) { Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)) });
            signature.Inlines.Add(new Run($" → {_returnType}") { Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)) });
            panel.Children.Add(signature);

            // 描述
            panel.Children.Add(new TextBlock
            {
                Text = _description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 11
            });

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(69, 69, 69)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                Child = panel
            };

            return border;
        }
    }

    public double Priority => 0;

    public virtual void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }

    /// <summary>
    /// 获取所有 FCom API 补全项
    /// </summary>
    public static IEnumerable<FComCompletionData> GetFComApiCompletions()
    {
        // 数据发送
        yield return new FComCompletionData("send", "发送十六进制数据", "boolean", "hexData: string");
        yield return new FComCompletionData("sendBytes", "发送原始字节数组", "boolean", "data: byte[]");
        yield return new FComCompletionData("sendText", "发送文本字符串 (UTF-8)", "boolean", "text: string");

        // 日志输出
        yield return new FComCompletionData("log", "输出普通日志 (Info)", "void", "message: string");
        yield return new FComCompletionData("logDebug", "输出调试日志 (Debug)", "void", "message: string");
        yield return new FComCompletionData("logWarning", "输出警告日志 (Warning)", "void", "message: string");
        yield return new FComCompletionData("logError", "输出错误日志 (Error)", "void", "message: string");

        // 延时
        yield return new FComCompletionData("delay", "阻塞延时 (支持中断)", "void", "ms: number");

        // 校验计算
        yield return new FComCompletionData("crc16", "计算 CRC16-Modbus", "string", "hexData: string");
        yield return new FComCompletionData("crc32", "计算 CRC32", "string", "hexData: string");
        yield return new FComCompletionData("checksum", "计算 Sum8 校验和", "string", "hexData: string");

        // 工具方法
        yield return new FComCompletionData("getTimestamp", "获取 Unix 时间戳 (毫秒)", "number");
        yield return new FComCompletionData("hexToBytes", "十六进制字符串转字节数组", "byte[]", "hexString: string");
        yield return new FComCompletionData("bytesToHex", "字节数组转十六进制字符串", "string", "data: byte[]");

        // 协议 API
        yield return new FComCompletionData("getProtocols", "获取所有已注册的协议名称列表", "string[]");
        yield return new FComCompletionData("getProtocolFields", "获取指定协议的所有数据项定义", "table[]", "protocolName: string");
        yield return new FComCompletionData("parse", "使用指定协议解析帧数据", "table", "protocolName: string, hexFrame: string");
        yield return new FComCompletionData("build", "使用指定协议构建帧数据", "string", "protocolName: string, fieldValues: table");
    }

    /// <summary>
    /// 获取 Lua 关键字补全项
    /// </summary>
    public static IEnumerable<FComCompletionData> GetLuaKeywordCompletions()
    {
        // 控制流
        yield return new FComCompletionData("if", "条件判断");
        yield return new FComCompletionData("then", "条件体开始");
        yield return new FComCompletionData("else", "否则分支");
        yield return new FComCompletionData("elseif", "否则如果");
        yield return new FComCompletionData("end", "块结束");
        yield return new FComCompletionData("for", "for 循环");
        yield return new FComCompletionData("while", "while 循环");
        yield return new FComCompletionData("do", "循环体开始");
        yield return new FComCompletionData("repeat", "repeat 循环");
        yield return new FComCompletionData("until", "repeat 条件");
        yield return new FComCompletionData("break", "跳出循环");
        yield return new FComCompletionData("return", "返回值");

        // 关键字
        yield return new FComCompletionData("local", "局部变量");
        yield return new FComCompletionData("function", "函数定义");
        yield return new FComCompletionData("and", "逻辑与");
        yield return new FComCompletionData("or", "逻辑或");
        yield return new FComCompletionData("not", "逻辑非");
        yield return new FComCompletionData("true", "布尔真");
        yield return new FComCompletionData("false", "布尔假");
        yield return new FComCompletionData("nil", "空值");
        yield return new FComCompletionData("in", "迭代器");

        // 全局对象
        yield return new FComCompletionData("FCom", "FlexCom 脚本 API 对象");

        // 常用全局函数
        yield return new FComCompletionData("print", "打印输出", "void", "...");
        yield return new FComCompletionData("type", "获取类型", "string", "value");
        yield return new FComCompletionData("tostring", "转为字符串", "string", "value");
        yield return new FComCompletionData("tonumber", "转为数字", "number?", "value");
        yield return new FComCompletionData("pairs", "遍历表", "iterator", "table");
        yield return new FComCompletionData("ipairs", "遍历数组", "iterator", "table");
        yield return new FComCompletionData("string", "字符串库");
        yield return new FComCompletionData("table", "表操作库");
        yield return new FComCompletionData("math", "数学库");
    }

    /// <summary>
    /// 获取协议名称补全项（上下文感知，含协议描述）
    /// </summary>
    public static IEnumerable<FComCompletionData> GetProtocolNameCompletions(IEnumerable<(string Name, string Description)> protocols)
    {
        foreach (var (name, description) in protocols)
        {
            var desc = string.IsNullOrEmpty(description) ? $"协议: {name}" : description;
            yield return new FComCompletionData(name, desc, "protocol");
        }
    }

    /// <summary>
    /// 获取 FCom.build 上下文中的协议名称补全项 - 选中后自动插入字段模板
    /// </summary>
    public static IEnumerable<BuildTemplateCompletionData> GetBuildProtocolCompletions(
        IEnumerable<(string Name, string Description, IEnumerable<string> FieldNames)> protocols)
    {
        foreach (var (name, description, fieldNames) in protocols)
        {
            var desc = string.IsNullOrEmpty(description) ? $"协议: {name}" : description;
            yield return new BuildTemplateCompletionData(name, desc, fieldNames.ToList());
        }
    }

    /// <summary>
    /// 获取协议字段名补全项（上下文感知）
    /// </summary>
    public static IEnumerable<FComCompletionData> GetProtocolFieldCompletions(IEnumerable<(string Name, string Description)> fields)
    {
        foreach (var (name, description) in fields)
        {
            yield return new FComCompletionData(name, string.IsNullOrEmpty(description) ? $"字段: {name}" : description);
        }
    }
}

/// <summary>
/// FCom.build 上下文中的协议名补全项 - 选中后自动插入字段模板代码
/// </summary>
public class BuildTemplateCompletionData : FComCompletionData
{
    private readonly List<string> _fieldNames;

    public BuildTemplateCompletionData(string protocolName, string description, List<string> fieldNames)
        : base(protocolName, description, "protocol")
    {
        _fieldNames = fieldNames;
    }

    public override void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // 插入协议名 + 字段模板
        var fieldEntries = _fieldNames.Select(f => $"[\"{f}\"] = \"\"");
        var template = $"{Text}\", {{ {string.Join(", ", fieldEntries)} }}";
        textArea.Document.Replace(completionSegment, template);
    }
}
