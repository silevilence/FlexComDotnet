using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>
/// ScriptingView.xaml 的交互逻辑
/// </summary>
public partial class ScriptingView : UserControl
{
    private readonly ScriptingViewModel _viewModel;
    private ScriptEditorWindow? _editorWindow;

    public ScriptingView(ScriptingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 初始化代码预览编辑器
        InitializeCodePreviewEditor();

        // 注入 WPF Dispatcher 调度器（Invoke 自动判断线程：UI 线程直接执行，后台线程同步切换）
        viewModel.DispatcherAction = action => Dispatcher.Invoke(action);

        // 监听 EditorContent 变化以更新预览
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 订阅打开编辑器请求
        viewModel.OpenEditorRequested += OnOpenEditorRequested;
    }

    private void InitializeCodePreviewEditor()
    {
        // 加载 Lua 语法高亮
        try
        {
            var assembly = typeof(ScriptingView).Assembly;
            var resourceName = "FlexComDotnet.Features.Scripting.Resources.LuaSyntax.xshd";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                CodePreviewEditor.SyntaxHighlighting = highlighting;
            }
        }
        catch
        {
            // 加载失败时使用默认无高亮
        }

        // 应用深色主题
        CodePreviewEditor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        CodePreviewEditor.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212));

        // 初始化内容
        CodePreviewEditor.Text = _viewModel.EditorContent;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScriptingViewModel.EditorContent))
        {
            CodePreviewEditor.Text = _viewModel.EditorContent;
        }
    }

    private void OnOpenEditorRequested(object? sender, EventArgs e)
    {
        OpenEditorWindow();
    }

    private void OpenEditorWindow()
    {
        // 如果编辑器窗口已打开，则激活它
        if (_editorWindow is { IsLoaded: true })
        {
            _editorWindow.Activate();
            return;
        }

        // 创建并显示编辑器窗口
        _editorWindow = new ScriptEditorWindow(_viewModel)
        {
            Owner = Window.GetWindow(this)
        };
        _editorWindow.Closed += (_, _) => _editorWindow = null;
        _editorWindow.Show();
    }

    #region 脚本列表右键菜单

    private void RunScript_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedScript == null) return;
        
        if (_viewModel.RunScriptCommand.CanExecute(null))
        {
            _viewModel.RunScriptCommand.Execute(null);
        }
    }

    private void EditScript_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedScript == null) return;
        OpenEditorWindow();
    }

    private void RenameScript_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedScript == null) return;

        var currentName = _viewModel.SelectedScript.Name;
        
        // 使用简单的输入对话框
        var dialog = new RenameDialog(currentName)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
        {
            var newName = dialog.NewName.Trim();
            if (newName != currentName)
            {
                _viewModel.RenameScript(newName);
            }
        }
    }

    private void DeleteScript_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedScript == null) return;

        var dialog = new DeleteConfirmDialog(_viewModel.SelectedScript.Name)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.DeleteScriptCommand.Execute(null);
        }
    }

    #endregion
}
