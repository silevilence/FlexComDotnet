using System.Windows;
using FlexComDotnet.Core.Features.Update.ViewModels;

namespace FlexComDotnet.Features.Update.Views;

/// <summary>
/// 更新窗口
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateViewModel _viewModel;

    public UpdateWindow(UpdateViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        DataContext = _viewModel;

        // 窗口加载时自动检查更新
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 自动检查更新
        if (_viewModel.CheckForUpdateCommand.CanExecute(null))
        {
            await _viewModel.CheckForUpdateCommand.ExecuteAsync(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Cleanup();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
