using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.ViewModels;

namespace FlexComDotnet;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 设置串口配置面板的 DataContext
        SerialConfigPanel.DataContext = App.Services.GetRequiredService<SerialConfigViewModel>();
        
        // 设置收发区域的 DataContext
        var communicationViewModel = App.Services.GetRequiredService<SerialCommunicationViewModel>();
        SerialCommunicationPanel.DataContext = communicationViewModel;
        
        // 设置指令列表面板的 DataContext
        var commandListViewModel = App.Services.GetRequiredService<CommandListViewModel>();
        CommandListPanel.DataContext = commandListViewModel;
        
        // 订阅指令列表的发送请求事件
        commandListViewModel.SendDataRequested += (sender, data) =>
        {
            communicationViewModel.SendData(data);
        };
    }
}
