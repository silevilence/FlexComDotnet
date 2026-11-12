using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using FlexComDotnet.Core.Features.Protocol.Services;
using FlexComDotnet.Core.Features.Scripting.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Extensions.DependencyInjection;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>
/// ScriptingView.xaml 的交互逻辑
/// </summary>
public partial class ScriptingView : UserControl
{
    private readonly ScriptingViewModel _viewModel;


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

        // 注入确认删除回调
        viewModel.ConfirmAction = message =>
            System.Windows.MessageBox.Show(message, "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
    }

    private void InitializeCodePreviewEditor()
    {
        LuaEditorTheme.Initialize(CodePreviewEditor);

        CodePreviewEditor.SetResourceReference(BackgroundProperty, "BackgroundBrush");
        CodePreviewEditor.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");

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
        if (_viewModel.SelectedScript is { } script && Application.Current.MainWindow is MainWindow main)
            main.OpenScriptEditor(script.Id);
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
