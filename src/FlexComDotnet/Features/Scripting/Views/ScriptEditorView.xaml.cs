using System.Windows.Controls;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using FlexComDotnet.Features.Scripting.Completion;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>
/// 脚本编辑器文档 - 支持 Lua 语法高亮和智能补全
/// </summary>
public partial class ScriptEditorView : UserControl
{
    private readonly ScriptingViewModel _viewModel;
    private readonly IProtocolParserService? _protocolParserService;
    private bool _isUpdatingFromViewModel;
    private bool _isUpdatingFromEditor;
    private CompletionWindow? _completionWindow;

    /// <summary>
    /// 匹配 FCom.parse/build/getProtocolFields 调用中协议名参数位置的正则
    /// 例如: FCom.parse("  或 FCom.build("  或 FCom.getProtocolFields("
    /// </summary>
    private static readonly Regex s_protocolNameContextRegex = new(
        @"FCom\.(parse|build|getProtocolFields)\(\s*""([^""]*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// 匹配 FCom.build 调用中字段名位置的正则
    /// 例如: FCom.build("Proto", { ["  或 , ["
    /// </summary>
    private static readonly Regex s_buildFieldContextRegex = new(
        @"FCom\.build\(\s*""([^""]+)""\s*,\s*\{.*\[\s*""([^""]*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// 匹配 .fields["  或 ["字段名" 访问模式（解析结果字段访问）
    /// </summary>
    private static readonly Regex s_fieldsAccessRegex = new(
        @"\.fields\[\s*""([^""]*)$",
        RegexOptions.Compiled);

    public ScriptEditorView(ScriptingViewModel viewModel, IProtocolParserService? protocolParserService = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _protocolParserService = protocolParserService;
        DataContext = viewModel;

        // 加载 Lua 语法高亮
        LuaEditorTheme.Initialize(CodeEditor);

        viewModel.DispatcherAction = action => Dispatcher.Invoke(action);

        // 应用编辑器主题
        ApplyEditorTheme();

        // 初始化编辑器内容
        CodeEditor.Text = viewModel.EditorContent;

        // 双向绑定编辑器内容
        CodeEditor.TextChanged += OnEditorTextChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 设置智能补全
        CodeEditor.TextArea.TextEntering += OnTextEntering;
        CodeEditor.TextArea.TextEntered += OnTextEntered;

        // 设置协议引用静态检查（红色下划线标记不存在的协议）
        if (_protocolParserService != null)
        {
            CodeEditor.TextArea.TextView.LineTransformers.Add(
                new ProtocolReferenceColorizer(_protocolParserService));
        }


    }

    private void ApplyEditorTheme()
    {
        CodeEditor.SetResourceReference(BackgroundProperty, "BackgroundBrush");
        CodeEditor.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        CodeEditor.SetResourceReference(ICSharpCode.AvalonEdit.TextEditor.LineNumbersForegroundProperty, "TextSecondaryBrush");

        // 设置选中文本颜色
        CodeEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "SelectedBrush");
        CodeEditor.TextArea.SelectionForeground = null;

        // 设置当前行高亮
        CodeEditor.TextArea.TextView.SetResourceReference(ICSharpCode.AvalonEdit.Rendering.TextView.CurrentLineBackgroundProperty, "HoverBrush");
    }

    private void OnTextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        // 输入 "." 后检查是否是 FCom.
        if (e.Text == ".")
        {
            var offset = CodeEditor.CaretOffset;
            if (offset >= 5)
            {
                var textBefore = CodeEditor.Document.GetText(offset - 5, 4);
                if (textBefore == "FCom")
                {
                    ShowFComApiCompletion();
                    return;
                }
            }
        }

        // 输入引号时检查是否在协议名参数位置
        if (e.Text == "\"" && _protocolParserService != null)
        {
            if (TryShowProtocolNameCompletion())
                return;
            if (TryShowProtocolFieldCompletion())
                return;
        }

        // 输入字母时显示关键字补全
        if (char.IsLetter(e.Text[0]))
        {
            // 先检查是否在协议名字符串中输入（继续补全）
            if (_protocolParserService != null && TryShowProtocolNameCompletion())
                return;

            // 检查是否在字段名字符串中输入（继续补全）
            if (_protocolParserService != null && TryShowProtocolFieldCompletion())
                return;

            var word = GetCurrentWord();
            if (word.Length >= 2)
            {
                ShowKeywordCompletion(word);
            }
        }
    }

    private string GetCurrentWord()
    {
        var offset = CodeEditor.CaretOffset;
        var document = CodeEditor.Document;
        var start = offset;

        while (start > 0)
        {
            var c = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_')
                break;
            start--;
        }

        return document.GetText(start, offset - start);
    }

    private void ShowFComApiCompletion()
    {
        _completionWindow = new CompletionWindow(CodeEditor.TextArea);
        ApplyCompletionWindowStyle(_completionWindow);
        
        // 获取 FCom. 后面输入的内容
        Func<string> getPrefixAfterDot = () =>
        {
            var offset = CodeEditor.CaretOffset;
            var document = CodeEditor.Document;
            
            // 找到 "." 的位置
            var dotPos = offset - 1;
            while (dotPos >= 0 && document.GetCharAt(dotPos) != '.')
            {
                dotPos--;
            }
            
            if (dotPos < 0 || dotPos + 1 >= offset) return string.Empty;
            return document.GetText(dotPos + 1, offset - dotPos - 1);
        };
        
        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var item in FComCompletionData.GetFComApiCompletions())
        {
            item.GetCurrentPrefix = getPrefixAfterDot;
            data.Add(item);
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private void ShowKeywordCompletion(string prefix)
    {
        if (_completionWindow != null) return;

        var matchingItems = FComCompletionData.GetLuaKeywordCompletions()
            .Where(item => item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingItems.Count == 0) return;

        _completionWindow = new CompletionWindow(CodeEditor.TextArea)
        {
            StartOffset = CodeEditor.CaretOffset - prefix.Length
        };
        ApplyCompletionWindowStyle(_completionWindow);

        // 动态获取当前输入的前缀
        Func<string> getCurrentPrefix = () => GetCurrentWord();

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var item in matchingItems)
        {
            item.GetCurrentPrefix = getCurrentPrefix;
            data.Add(item);
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    /// <summary>
    /// 尝试检测光标是否在 FCom.parse/build/getProtocolFields 的协议名参数位置，
    /// 如果是则显示协议名称补全
    /// </summary>
    private bool TryShowProtocolNameCompletion()
    {
        if (_completionWindow != null) return false;
        if (_protocolParserService == null) return false;

        var offset = CodeEditor.CaretOffset;
        var line = CodeEditor.Document.GetLineByOffset(offset);
        var lineText = CodeEditor.Document.GetText(line.Offset, offset - line.Offset);

        var match = s_protocolNameContextRegex.Match(lineText);
        if (!match.Success) return false;

        var typedPrefix = match.Groups[2].Value; // 引号后已输入的部分
        ShowProtocolNameCompletion(typedPrefix);
        return true;
    }

    /// <summary>
    /// 显示协议名称补全列表
    /// </summary>
    private void ShowProtocolNameCompletion(string typedPrefix)
    {
        var definitions = _protocolParserService!.GetAllDefinitions();

        // 检查是否在 FCom.build 上下文中 - 提供模板补全
        var offset = CodeEditor.CaretOffset;
        var line = CodeEditor.Document.GetLineByOffset(offset);
        var lineText = CodeEditor.Document.GetText(line.Offset, offset - line.Offset);
        var isBuildContext = lineText.Contains("FCom.build(", StringComparison.Ordinal);

        List<FComCompletionData> matchingItems;

        if (isBuildContext)
        {
            var protocols = definitions.Select(d =>
            {
                var p = _protocolParserService!.GetParser(d.Name);
                var fields = p != null
                    ? p.GetBuildFieldInputs().Select(f => (f.FieldName, f.DefaultValue))
                    : d.Fields.Where(f => f.IsEnabled).Select(f => (f.Name, DefaultValue: string.Empty));
                return (d.Name, Description: d.Description ?? string.Empty, Fields: fields);
            });
            matchingItems = FComCompletionData.GetBuildProtocolCompletions(protocols)
                .Where(item => item.Text.StartsWith(typedPrefix, StringComparison.OrdinalIgnoreCase))
                .Cast<FComCompletionData>()
                .ToList();
        }
        else
        {
            var protocols = definitions.Select(d => (d.Name, Description: d.Description ?? string.Empty));
            matchingItems = FComCompletionData.GetProtocolNameCompletions(protocols)
                .Where(item => item.Text.StartsWith(typedPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (matchingItems.Count == 0) return;

        _completionWindow = new CompletionWindow(CodeEditor.TextArea)
        {
            StartOffset = CodeEditor.CaretOffset - typedPrefix.Length
        };
        ApplyCompletionWindowStyle(_completionWindow);

        Func<string> getCurrentPrefix = () =>
        {
            var currentOffset = CodeEditor.CaretOffset;
            var currentLine = CodeEditor.Document.GetLineByOffset(currentOffset);
            var currentLineText = CodeEditor.Document.GetText(currentLine.Offset, currentOffset - currentLine.Offset);
            var m = s_protocolNameContextRegex.Match(currentLineText);
            return m.Success ? m.Groups[2].Value : string.Empty;
        };

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var item in matchingItems)
        {
            item.GetCurrentPrefix = getCurrentPrefix;
            data.Add(item);
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    /// <summary>
    /// 尝试检测光标是否在字段名位置（FCom.build 的字段参数 或 .fields["xxx"] 访问），
    /// 如果是则显示字段名补全
    /// </summary>
    private bool TryShowProtocolFieldCompletion()
    {
        if (_completionWindow != null) return false;
        if (_protocolParserService == null) return false;

        var offset = CodeEditor.CaretOffset;
        var line = CodeEditor.Document.GetLineByOffset(offset);
        var lineText = CodeEditor.Document.GetText(line.Offset, offset - line.Offset);

        // 检查 FCom.build("Proto", { ["fieldPrefix 模式
        var buildMatch = s_buildFieldContextRegex.Match(lineText);
        if (buildMatch.Success)
        {
            var protocolName = buildMatch.Groups[1].Value;
            var typedPrefix = buildMatch.Groups[2].Value;
            ShowProtocolFieldCompletion(protocolName, typedPrefix);
            return true;
        }

        // 检查 .fields["fieldPrefix 模式 - 需要从上下文推断协议名
        var fieldsMatch = s_fieldsAccessRegex.Match(lineText);
        if (fieldsMatch.Success)
        {
            var typedPrefix = fieldsMatch.Groups[1].Value;
            var protocolName = FindProtocolNameInContext();
            if (protocolName != null)
            {
                ShowProtocolFieldCompletion(protocolName, typedPrefix);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从当前文档上下文中查找最近使用的协议名（向上搜索 FCom.parse/build 调用）
    /// </summary>
    private string? FindProtocolNameInContext()
    {
        var offset = CodeEditor.CaretOffset;
        var line = CodeEditor.Document.GetLineByOffset(offset);
        var searchRegex = new Regex(@"FCom\.(parse|build)\(\s*""([^""]+)""", RegexOptions.Compiled);

        // 从当前行向上搜索最近的协议引用（最多查找 20 行）
        for (int i = 0; i < 20 && line != null; i++)
        {
            var lineText = CodeEditor.Document.GetText(line.Offset, line.Length);
            var match = searchRegex.Match(lineText);
            if (match.Success)
            {
                return match.Groups[2].Value;
            }
            line = line.PreviousLine;
        }

        return null;
    }

    /// <summary>
    /// 显示协议字段名补全列表
    /// </summary>
    private void ShowProtocolFieldCompletion(string protocolName, string typedPrefix)
    {
        var parser = _protocolParserService!.GetParser(protocolName);
        if (parser == null) return;

        var fields = parser.GetBuildFieldInputs()
            .Select(f =>
            {
                var desc = string.IsNullOrEmpty(f.Description) ? $"{f.DataType}" : f.Description;
                if (!string.IsNullOrEmpty(f.DefaultValue))
                    desc += $" (示例: {f.DefaultValue})";
                return (f.FieldName, Description: desc);
            });

        var matchingItems = FComCompletionData.GetProtocolFieldCompletions(fields)
            .Where(item => item.Text.StartsWith(typedPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingItems.Count == 0) return;

        _completionWindow = new CompletionWindow(CodeEditor.TextArea)
        {
            StartOffset = CodeEditor.CaretOffset - typedPrefix.Length
        };
        ApplyCompletionWindowStyle(_completionWindow);

        Func<string> getCurrentPrefix = () =>
        {
            var currentOffset = CodeEditor.CaretOffset;
            var currentLine = CodeEditor.Document.GetLineByOffset(currentOffset);
            var currentLineText = CodeEditor.Document.GetText(currentLine.Offset, currentOffset - currentLine.Offset);
            // 获取最后一个 [" 后的文本
            var lastBracketQuote = currentLineText.LastIndexOf("[\"", StringComparison.Ordinal);
            return lastBracketQuote >= 0 ? currentLineText[(lastBracketQuote + 2)..] : string.Empty;
        };

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var item in matchingItems)
        {
            item.GetCurrentPrefix = getCurrentPrefix;
            data.Add(item);
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private static void ApplyCompletionWindowStyle(CompletionWindow window)
    {
        window.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        window.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        window.SetResourceReference(BorderBrushProperty, "BorderBrush");
        window.BorderThickness = new Thickness(1);
        
        // 设置补全列表样式
        window.CompletionList.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        window.CompletionList.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        
        // 设置窗口大小，避免滚动条
        window.Width = 380;
        window.MaxHeight = 300;
        window.MinWidth = 380;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel) return;

        _isUpdatingFromEditor = true;
        _viewModel.EditorContent = CodeEditor.Text;
        _isUpdatingFromEditor = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScriptingViewModel.EditorContent) && !_isUpdatingFromEditor)
        {
            _isUpdatingFromViewModel = true;
            if (CodeEditor.Text != _viewModel.EditorContent)
            {
                CodeEditor.Text = _viewModel.EditorContent;
            }
            _isUpdatingFromViewModel = false;
        }
    }

    private void OnOpenApiReference_Click(object sender, RoutedEventArgs e)
    {
        var apiWindow = new ApiReferenceWindow
        {
            Owner = Window.GetWindow(this)
        };
        apiWindow.Show();
    }

    public Task<bool> RequestCloseAsync()
    {
        if (!_viewModel.IsEditorDirty) return Task.FromResult(true);
        var result = MessageBox.Show(Window.GetWindow(this), "脚本尚未保存。是否保存后关闭？", "保存脚本",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel) return Task.FromResult(false);
        if (result == MessageBoxResult.No) return Task.FromResult(true);
        _viewModel.SaveScriptCommand.Execute(null);
        return Task.FromResult(!_viewModel.IsEditorDirty);
    }

    public void Release()
    {
        CodeEditor.TextArea.TextEntering -= OnTextEntering;
        CodeEditor.TextArea.TextEntered -= OnTextEntered;
        CodeEditor.TextChanged -= OnEditorTextChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _completionWindow?.Close();
        _viewModel.Dispose();
    }
}
