using System.Collections.Specialized;
using System.Windows;
using FlexComDotnet.Core.Features.Scripting.ViewModels;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>
/// 脚本编辑器弹出窗口
/// </summary>
public partial class ScriptEditorWindow : Window
{
    public ScriptEditorWindow(ScriptingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

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
    }
}
