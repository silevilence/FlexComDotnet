using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using FlexComDotnet.Core.Features.Scripting.ViewModels;

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

        // 注入 WPF Dispatcher 调度器（Invoke 自动判断线程：UI 线程直接执行，后台线程同步切换）
        viewModel.DispatcherAction = action => Dispatcher.Invoke(action);

        // 日志自动滚动到底部（CollectionChanged 可能从后台线程触发，需要通过 Dispatcher 访问 UI 控件）
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

        // 订阅打开编辑器请求
        viewModel.OpenEditorRequested += OnOpenEditorRequested;
    }

    private void OnOpenEditorRequested(object? sender, EventArgs e)
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
}
