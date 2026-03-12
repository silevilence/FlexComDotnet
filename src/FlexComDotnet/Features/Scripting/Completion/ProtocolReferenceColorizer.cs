using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using FlexComDotnet.Core.Features.Protocol.Services;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace FlexComDotnet.Features.Scripting.Completion;

/// <summary>
/// 协议引用静态检查着色器 - 对脚本中引用了不存在协议的位置标红色下划线
/// </summary>
public partial class ProtocolReferenceColorizer : DocumentColorizingTransformer
{
    private readonly IProtocolParserService _protocolParserService;

    /// <summary>
    /// 匹配 FCom.parse/build/getProtocolFields 中的第一个字符串参数
    /// 捕获组 1: 引号内的协议名
    /// 捕获组位置: 用于精确着色引号内的文本
    /// </summary>
    [GeneratedRegex(@"FCom\.(parse|build|getProtocolFields)\(\s*""([^""]*?)""")]
    private static partial Regex ProtocolRefPattern();

    private static readonly Pen s_errorUnderlinePen = CreateErrorPen();

    public ProtocolReferenceColorizer(IProtocolParserService protocolParserService)
    {
        _protocolParserService = protocolParserService;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);
        var matches = ProtocolRefPattern().Matches(lineText);

        foreach (Match match in matches)
        {
            var protocolNameGroup = match.Groups[2];
            var protocolName = protocolNameGroup.Value;

            if (string.IsNullOrEmpty(protocolName)) continue;

            // 检查协议是否存在
            var parser = _protocolParserService.GetParser(protocolName);
            if (parser != null) continue;

            // 协议不存在 → 标红色下划线
            var startOffset = line.Offset + protocolNameGroup.Index;
            var endOffset = startOffset + protocolNameGroup.Length;

            ChangeLinePart(startOffset, endOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(
                    new SolidColorBrush(Color.FromRgb(244, 71, 71))); // 红色文字
                var decoration = new TextDecoration
                {
                    Location = TextDecorationLocation.Underline,
                    Pen = s_errorUnderlinePen
                };
                element.TextRunProperties.SetTextDecorations(
                    new TextDecorationCollection([decoration]));
            });
        }
    }

    private static Pen CreateErrorPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(244, 71, 71)), 1.5)
        {
            DashStyle = DashStyles.Dot
        };
        pen.Freeze();
        return pen;
    }
}
