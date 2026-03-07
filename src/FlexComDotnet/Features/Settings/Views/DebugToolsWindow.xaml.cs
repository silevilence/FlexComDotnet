using System.Windows;
using FlexComDotnet.Core.Features.Settings.ViewModels;

namespace FlexComDotnet.Features.Settings.Views;

/// <summary>
/// 调试工具窗口
/// </summary>
public partial class DebugToolsWindow : Window
{
    public DebugToolsWindow(DebugToolsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
