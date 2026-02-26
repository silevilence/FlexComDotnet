using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using FlexComDotnet.Features.Scripting.Completion;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>
/// 脚本编辑器弹出窗口 - 支持 Lua 语法高亮和智能补全
/// </summary>
public partial class ScriptEditorWindow : Window
{
    private readonly ScriptingViewModel _viewModel;
    private bool _isUpdatingFromViewModel;
    private bool _isUpdatingFromEditor;
    private CompletionWindow? _completionWindow;

    public ScriptEditorWindow(ScriptingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 加载 Lua 语法高亮
        LoadLuaSyntaxHighlighting();

        // 应用深色主题样式
        ApplyEditorTheme();

        // 初始化编辑器内容
        CodeEditor.Text = viewModel.EditorContent;

        // 双向绑定编辑器内容
        CodeEditor.TextChanged += OnEditorTextChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 设置智能补全
        CodeEditor.TextArea.TextEntering += OnTextEntering;
        CodeEditor.TextArea.TextEntered += OnTextEntered;

        // 日志自动滚动到底部
        if (viewModel.LogEntries is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (_, _) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                    }
                });
            };
        }

        Closed += OnWindowClosed;
    }

    private void LoadLuaSyntaxHighlighting()
    {
        try
        {
            var assembly = typeof(ScriptEditorWindow).Assembly;
            var resourceName = "FlexComDotnet.Features.Scripting.Resources.LuaSyntax.xshd";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                CodeEditor.SyntaxHighlighting = highlighting;
            }
        }
        catch
        {
            // 加载失败时使用默认无高亮
        }
    }

    private void ApplyEditorTheme()
    {
        // 深色主题配色
        CodeEditor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        CodeEditor.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212));
        CodeEditor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(133, 133, 133));

        // 设置选中文本颜色
        CodeEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 51, 153, 255));
        CodeEditor.TextArea.SelectionForeground = null;

        // 设置当前行高亮
        CodeEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
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

        // 输入字母时显示关键字补全
        if (char.IsLetter(e.Text[0]))
        {
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

    private static void ApplyCompletionWindowStyle(CompletionWindow window)
    {
        // 深色主题样式
        window.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
        window.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212));
        window.BorderBrush = new SolidColorBrush(Color.FromRgb(69, 69, 69));
        window.BorderThickness = new Thickness(1);
        
        // 设置补全列表样式
        window.CompletionList.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
        window.CompletionList.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212));
        
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
            Owner = this
        };
        apiWindow.Show();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CodeEditor.TextArea.TextEntering -= OnTextEntering;
        CodeEditor.TextArea.TextEntered -= OnTextEntered;
        CodeEditor.TextChanged -= OnEditorTextChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        Owner?.Activate();
    }
}
